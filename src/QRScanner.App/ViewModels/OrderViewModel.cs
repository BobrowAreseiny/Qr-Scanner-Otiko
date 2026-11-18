using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services;
using QRScanner.App.Services.Datamark;
using QRScanner.Core.Barcodes;

namespace QRScanner.App.ViewModels;

public sealed partial class OrderViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IApiSettingsService _apiSettings;
    private readonly IDatamarkApiClient _apiClient;
    private readonly IOneCSettingsService _oneCSettings;
    private readonly IOneCApiClient _oneCClient;
    private readonly IOrderFileExporter _orderFileExporter;
    private readonly ITransferQueueService _transferQueue;

    public OrderViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient,
        IOneCSettingsService oneCSettings,
        IOneCApiClient oneCClient,
        IOrderFileExporter orderFileExporter,
        ITransferQueueService transferQueue)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;
        _apiSettings = apiSettings;
        _apiClient = apiClient;
        _oneCSettings = oneCSettings;
        _oneCClient = oneCClient;
        _orderFileExporter = orderFileExporter;
        _transferQueue = transferQueue;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        DeleteLineCommand = new AsyncRelayCommand<OrderLineEntity>(DeleteLineAsync);
        OpenSettingsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(QRScanner.App.Views.SettingsPage)));
    }

    public ObservableCollection<OrderLineEntity> Lines { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand<OrderLineEntity> DeleteLineCommand { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }

    [ObservableProperty]
    private bool isAuthRequired;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string countText = "0 марок";

    [ObservableProperty]
    private string statusMessage = "Ожидание сканирования";

    [ObservableProperty]
    private bool isBusy;

    public Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = $"Заказ {DateTime.Now:dd.MM.yyyy HH-mm}";
        }

        return Task.CompletedTask;
    }

    public async Task HandleBarcodeAsync(BarcodeScannedEventArgs args)
    {
        if (IsBusy)
        {
            return;
        }

        var code = BarcodeParser.Normalize(args.Data);
        if (code.Length == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (BarcodeParser.IsCorobCode(code))
            {
                await AddCorobAsync(code);
            }
            else
            {
                await AddMarkAsync(code);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сканирования: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddCorobAsync(string code)
    {
        if (!int.TryParse(code, out var corobId))
        {
            StatusMessage = "Некорректный код короба.";
            return;
        }

        var corob = await _database.GetCorobAsync(corobId);
        if (corob is null)
        {
            var corobs = await _database.GetCorobsAsync();
            corob = corobs.FirstOrDefault(item =>
                string.Equals(item.Name.Trim(), code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Name.Trim(), $"№{corobId}", StringComparison.OrdinalIgnoreCase));
        }

        if (corob is null)
        {
            StatusMessage = $"Короб №{corobId} не найден.";
            return;
        }

        if (corob.IsShipped)
        {
            var order = await _database.GetShippedOrderForCorobAsync(corob.Id);
            var message = $"Короб {corob.Name} уже отгружен.";
            if (order is not null)
            {
                message +=
                    $"\nЗаказ: «{order.Name}» от {order.CreatedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}.";
            }

            StatusMessage = $"Короб {corob.Name} уже отгружен.";
            await _dialogs.ShowAlertAsync("Короб уже отгружен", message, "OK");
            return;
        }

        if (Lines.Any(l => l.SourceCorobId == corob.Id))
        {
            StatusMessage = $"Короб №{corobId} уже в заказе.";
            return;
        }

        var barcodes = await _database.GetBarcodesAsync(corob.Id);
        if (barcodes.Count == 0)
        {
            StatusMessage = $"В коробе №{corobId} нет марок.";
            return;
        }

        var added = 0;
        foreach (var barcode in barcodes)
        {
            if (Lines.Any(l => l.Barcode == barcode.Barcode))
            {
                continue;
            }

            Lines.Add(new OrderLineEntity
            {
                Barcode = barcode.Barcode,
                Gtin = barcode.Gtin,
                VendorCode = barcode.VendorCode,
                Size = barcode.Size,
                ProductName = barcode.ProductName,
                ProductGroup = barcode.ProductGroup,
                IsMyProduct = barcode.IsMyProduct,
                GtinCheck = barcode.GtinCheck,
                SourceCorobId = corob.Id,
                AddedAtUtc = DateTime.UtcNow
            });
            added++;
        }

        UpdateCount();
        StatusMessage = added == 0
            ? $"Марки короба №{corobId} уже есть в заказе."
            : $"Добавлен короб №{corobId}: {FormatCount(added)}.";
    }

    private async Task AddMarkAsync(string code)
    {
        var settings = await _apiSettings.LoadAsync();
        if (!settings.HasCredentials)
        {
            IsAuthRequired = true;
            StatusMessage = "Требуется авторизация Электронного знака. Укажите логин и пароль в настройках.";
            return;
        }

        IsAuthRequired = false;

        if (Lines.Any(l => l.Barcode == code))
        {
            StatusMessage = "Эта марка уже есть в заказе.";
            return;
        }

        var existing = await _database.FindBarcodeAsync(code);
        if (existing is not null)
        {
            var message = await BarcodeConflictInfo.BuildAsync(
                _database,
                _apiClient,
                existing,
                settings);
            StatusMessage = "Эта марка уже отсканирована ранее.";
            await _dialogs.ShowAlertAsync("Марка уже в коробе", message, "OK");
            return;
        }

        var gtin = BarcodeParser.ExtractGtin(code);
        string? vendorCode = null;
        string? size = null;
        string? productName = null;
        string? productGroup = null;
        bool? isMyProduct = null;
        bool? gtinCheck = null;
        LabelCheckResult? label = null;
        string? labelError = null;

        if (!string.IsNullOrWhiteSpace(gtin))
        {
            StatusMessage = "Запрос информации о товаре...";
            try
            {
                var product = await _apiClient.FindProductAsync(gtin!, settings);
                if (product is not null)
                {
                    vendorCode = product.VendorCode;
                    size = product.Size;
                    productName = product.Name;
                    productGroup = product.Group;
                    isMyProduct = product.IsMy;
                    gtinCheck = product.GtinCheck;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Электронный знак недоступен: {ex.Message}";
            }

            try
            {
                label = await _apiClient.CheckLabelAsync(code, settings);
            }
            catch (Exception ex)
            {
                labelError = ex.Message;
            }
        }

        if (!await DatamarkLabelStatus.ConfirmAddAsync(_dialogs, label))
        {
            StatusMessage = "Марка не добавлена.";
            return;
        }

        Lines.Add(new OrderLineEntity
        {
            Barcode = code,
            Gtin = gtin,
            VendorCode = vendorCode,
            Size = size,
            ProductName = productName,
            ProductGroup = productGroup,
            IsMyProduct = isMyProduct,
            GtinCheck = gtinCheck,
            AddedAtUtc = DateTime.UtcNow
        });

        UpdateCount();
        var status = productName is not null
            ? $"Товар: {productName}"
            : vendorCode is null && size is null
                ? "Марка добавлена. Информация о товаре не получена."
                : $"Товар: {vendorCode} {size}".Trim();

        if (label is not null && label.Found && DatamarkLabelStatus.IsNotInStock(label.Status))
        {
            status += $" Электронный знак: {DatamarkLabelStatus.ShortStatus(label.Status)}.";
        }
        else if (!string.IsNullOrWhiteSpace(labelError))
        {
            status += $" Проверка Электронного знака: {labelError}";
        }

        StatusMessage = status;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Введите название файла.");
            return;
        }

        if (Lines.Count == 0)
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Отсканируйте короб или марки.");
            return;
        }

        IsBusy = true;
        try
        {
            var order = await _database.SaveOrderAsync(new OrderEntity
            {
                Name = Name.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                IsShipped = true,
                ExternalId = Guid.NewGuid().ToString("D")
            });

            foreach (var line in Lines)
            {
                line.OrderId = order.Id;
                await _database.SaveOrderLineAsync(line);
            }

            var sourceCorobIds = Lines
                .Where(l => l.SourceCorobId.HasValue)
                .Select(l => l.SourceCorobId!.Value)
                .Distinct()
                .ToList();

            foreach (var corobId in sourceCorobIds)
            {
                var corob = await _database.GetCorobAsync(corobId);
                if (corob is null)
                {
                    continue;
                }

                corob.IsShipped = true;
                await _database.SaveCorobAsync(corob);
            }

            var export = await ExportAsync(Name.Trim(), Lines.Select(l => l.Barcode));
            var oneCStatus = await TrySendTo1CAsync(order);

            var queueNote = string.Empty;
            try
            {
                await _transferQueue.EnqueueAsync(
                    export.Location,
                    export.FileName,
                    TransferKinds.Order);
                queueNote = " Добавлено в очередь на компьютер.";
            }
            catch (Exception ex)
            {
                queueNote = $" Не удалось добавить в очередь: {ex.Message}";
            }

            StatusMessage = $"Сохранено: {export.FileName}. {oneCStatus}{queueNote}";
            var locationText = export.IsPublic
                ? $"Файл сохранён:\n{export.DisplayPath}"
                : $"Файл сохранён во внутренней папке:\n{export.DisplayPath}";

            await _dialogs.ShowAlertAsync(
                "Заказ",
                $"{locationText}\n\n{oneCStatus}{queueNote}",
                "OK");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось сохранить заказ: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string> TrySendTo1CAsync(OrderEntity order)
    {
        var settings = await _oneCSettings.LoadAsync();
        if (!settings.IsConfigured)
        {
            return "1С: API не настроен.";
        }

        try
        {
            OneCOrderRequestFactory.EnsureExternalId(order);
            var request = OneCOrderRequestFactory.Create(order, Lines, settings.Warehouse);
            var result = await _oneCClient.SendOrderAsync(settings, request);

            if (result.Success)
            {
                order.IsSentTo1C = true;
                order.SentTo1CAtUtc = DateTime.UtcNow;
                order.OneCNumber = result.Number;
                order.OneCError = null;
                await _database.SaveOrderAsync(order);
                return result.Message;
            }

            order.IsSentTo1C = false;
            order.OneCError = result.Message;
            await _database.SaveOrderAsync(order);
            return $"1С: {result.Message}";
        }
        catch (Exception ex)
        {
            order.IsSentTo1C = false;
            order.OneCError = ex.Message;
            await _database.SaveOrderAsync(order);
            return $"1С: ошибка отправки: {ex.Message}";
        }
    }

    private async Task DeleteLineAsync(OrderLineEntity? line)
    {
        if (line is null)
        {
            return;
        }

        if (line.Id != 0)
        {
            await _database.DeleteOrderLineAsync(line.Id);
        }

        Lines.Remove(line);
        UpdateCount();
        StatusMessage = "Марка удалена из заказа";
    }

    private Task<OrderFileExportResult> ExportAsync(
        string name,
        IEnumerable<string> barcodes)
    {
        return _orderFileExporter.ExportAsync(name, barcodes);
    }

    private void UpdateCount()
    {
        CountText = FormatCount(Lines.Count);
    }

    private static string FormatCount(int count)
    {
        var lastDigit = count % 10;
        var lastTwo = count % 100;

        if (lastDigit == 1 && lastTwo != 11)
        {
            return $"{count} марка";
        }

        if (lastDigit is >= 2 and <= 4 && (lastTwo < 12 || lastTwo > 14))
        {
            return $"{count} марки";
        }

        return $"{count} марок";
    }
}
