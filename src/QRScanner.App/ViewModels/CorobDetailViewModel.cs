using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services;
using QRScanner.App.Services.Datamark;
using QRScanner.Core.Barcodes;

namespace QRScanner.App.ViewModels;

public sealed partial class CorobDetailViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IApiSettingsService _apiSettings;
    private readonly IDatamarkApiClient _apiClient;
    private readonly IQrCodeService _qrCodeService;
    private readonly IFileTransferService _fileTransferService;
    private readonly ITransferQueueService _transferQueue;

    public CorobDetailViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient,
        IQrCodeService qrCodeService,
        IFileTransferService fileTransferService,
        ITransferQueueService transferQueue)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;
        _apiSettings = apiSettings;
        _apiClient = apiClient;
        _qrCodeService = qrCodeService;
        _fileTransferService = fileTransferService;
        _transferQueue = transferQueue;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        DeleteBarcodeCommand = new AsyncRelayCommand<BoxBarcodeEntity>(DeleteBarcodeAsync);
        OpenSettingsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(QRScanner.App.Views.SettingsPage)));
        ShareQrCommand = new AsyncRelayCommand(ShareQrAsync);
        SaveQrToFileCommand = new AsyncRelayCommand(SaveQrToFileAsync);
        QrMenuCommand = new AsyncRelayCommand(SaveQrAsync);
        EditNameCommand = new AsyncRelayCommand(EditNameAsync);
    }

    public ObservableCollection<BoxBarcodeEntity> Barcodes { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand<BoxBarcodeEntity> DeleteBarcodeCommand { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }

    public IAsyncRelayCommand ShareQrCommand { get; }

    public IAsyncRelayCommand SaveQrToFileCommand { get; }

    public IAsyncRelayCommand QrMenuCommand { get; }

    public IAsyncRelayCommand EditNameCommand { get; }

    [ObservableProperty]
    private CorobEntity? corob;

    [ObservableProperty]
    private bool isAuthRequired;

    [ObservableProperty]
    private string title = "Короб";

    [ObservableProperty]
    private string corobName = string.Empty;

    [ObservableProperty]
    private string countText = "0 / 0";

    [ObservableProperty]
    private string statusMessage = "Ожидание сканирования";

    public async Task LoadAsync(int id)
    {
        var corob = await _database.GetCorobAsync(id);
        if (corob is null)
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Короб не найден.");
            await _navigation.GoBackAsync();
            return;
        }

        Corob = corob;
        Title = corob.Name;
        CorobName = corob.Name;
        Barcodes.Clear();

        var barcodes = await _database.GetBarcodesAsync(corob.Id);
        foreach (var barcode in barcodes)
        {
            Barcodes.Add(barcode);
        }

        UpdateCount();

        var settings = await _apiSettings.LoadAsync();
        IsAuthRequired = !settings.HasCredentials;
        if (IsAuthRequired)
        {
            StatusMessage = "Требуется авторизация. Войдите в настройках.";
        }
    }

    public async Task HandleBarcodeAsync(BarcodeScannedEventArgs args)
    {
        if (Corob is null)
        {
            return;
        }

        var code = BarcodeParser.Normalize(args.Data);
        if (code.Length == 0)
        {
            return;
        }

        if (BarcodeParser.IsCorobCode(code))
        {
            StatusMessage = "Это код короба, а не марка товара.";
            return;
        }

        var settings = await _apiSettings.LoadAsync();
        if (!settings.HasCredentials)
        {
            IsAuthRequired = true;
            StatusMessage = "Требуется авторизация. Войдите в настройках.";
            return;
        }

        IsAuthRequired = false;

        if (Barcodes.Any(b => b.Barcode == code))
        {
            StatusMessage = "Эта марка уже есть в коробе.";
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
            StatusMessage = "Эта марка уже отсканирована в другом коробе.";
            await _dialogs.ShowAlertAsync("Марка уже в коробе", message, "OK");
            return;
        }

        if (Corob.Capacity > 0 && Barcodes.Count >= Corob.Capacity)
        {
            StatusMessage = $"Короб заполнен: {Barcodes.Count} из {Corob.Capacity}.";
            return;
        }

        var gtin = BarcodeParser.ExtractGtin(code);
        if (!string.IsNullOrWhiteSpace(gtin) && !GtinValidator.IsValid(gtin))
        {
            StatusMessage = "GTIN не прошёл проверку контрольной суммы.";
            return;
        }

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
            catch (DatamarkApiException ex) when (ex.StatusCode is 401 or 403)
            {
                IsAuthRequired = true;
                StatusMessage = "Требуется авторизация Электронного знака. Проверьте логин и пароль в настройках.";
                return;
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

        Barcodes.Add(new BoxBarcodeEntity
        {
            CorobId = Corob.Id,
            Barcode = code,
            Gtin = gtin,
            VendorCode = vendorCode,
            Size = size,
            ProductName = productName,
            ProductGroup = productGroup,
            IsMyProduct = isMyProduct,
            GtinCheck = gtinCheck,
            ScannedAtUtc = DateTime.UtcNow
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

    private async Task EditNameAsync()
    {
        if (Corob is null)
        {
            return;
        }

        var name = await _dialogs.ShowPromptAsync(
            "Название короба",
            "Введите название короба.",
            "Название",
            Corob.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var oldName = Corob.Name;

        try
        {
            var trimmed = name.Trim();
            if (string.Equals(oldName, trimmed, StringComparison.Ordinal))
            {
                return;
            }

            Corob.Name = trimmed;
            CorobName = trimmed;
            Title = trimmed;
            await _database.SaveCorobAsync(Corob);

            try
            {
                await _qrCodeService.DeleteAsync(Corob.Id, oldName);
                await _transferQueue.RemovePendingQrAsync(Corob.Id, oldName);

                var qr = await _qrCodeService.SaveToDownloadsAsync(
                    Corob.Id,
                    Corob.Name);

                if (!string.IsNullOrWhiteSpace(qr.Location))
                {
                    await _transferQueue.EnqueueAsync(
                        qr.Location,
                        qr.FileName,
                        TransferKinds.Qr,
                        Corob.Id);
                }

                StatusMessage = "Название сохранено. QR-код обновлён.";
            }
            catch (Exception ex)
            {
                StatusMessage =
                    "Название сохранено, но QR не обновлён: " + ex.Message;
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось сохранить название: {ex.Message}");
        }
    }

    private async Task SaveAsync()
    {
        if (Corob is null)
        {
            return;
        }

        var name = Corob.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Введите название короба.");
            return;
        }

        try
        {
            Corob.Name = name;
            await _database.SaveCorobAsync(Corob);

            foreach (var barcode in Barcodes.Where(b => b.Id == 0))
            {
                barcode.CorobId = Corob.Id;
                await _database.SaveBarcodeAsync(barcode);
            }

            StatusMessage = "Сохранено";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось сохранить короб: {ex.Message}");
        }
    }

    private async Task SaveQrAsync()
    {
        if (Corob is null)
        {
            return;
        }

        try
        {
            StatusMessage = "Сохранение QR-кода...";
            var result = await _qrCodeService.GetOrCreateAsync(
                Corob.Id,
                Corob.Name);

            var queueNote = string.Empty;
            if (result.IsPublic)
            {
                try
                {
                    await _transferQueue.EnqueueAsync(
                        result.Location,
                        result.FileName,
                        TransferKinds.Qr,
                        Corob.Id);
                    queueNote = " Добавлен в очередь на компьютер.";
                }
                catch (Exception ex)
                {
                    queueNote = $" Не удалось добавить в очередь: {ex.Message}";
                }
            }

            var prefix = result.Created ? "QR сохранён" : "QR уже сохранён";
            StatusMessage = $"{prefix}: {result.DisplayPath}.{queueNote}";
            await _dialogs.ShowAlertAsync(
                "QR-код",
                $"{(result.Created ? "Файл сохранён" : "Файл уже был сохранён")}:\n{result.DisplayPath}\n\n" +
                "Для отправки на компьютер нажмите «Отправка» на главном экране.",
                "OK");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка QR: {ex.Message}";
            await _dialogs.ShowAlertAsync(
                "QR-код",
                $"Не удалось сохранить QR: {ex.Message}");
        }
    }

    private async Task ShareQrAsync()
    {
        if (Corob is null)
        {
            return;
        }

        try
        {
            StatusMessage = "Генерация QR-кода...";
            var path = await _qrCodeService.GenerateAsync(Corob.Id, Corob.Name);
            var queueNote = await TryEnqueueQrAsync(path);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"QR-код короба {Corob.Name}",
                File = new ShareFile(path, "image/png")
            });

            StatusMessage = "QR-код готов." + queueNote;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка QR: {ex.Message}";
            var saveToFile = await _dialogs.ShowConfirmationAsync(
                "QR-код",
                "Не удалось поделиться QR-кодом. Сохранить его в файл?",
                "Сохранить",
                "Отмена");

            if (saveToFile)
            {
                await SaveQrToFileAsync();
            }
        }
    }

    private async Task ShowQrMenuAsync()
    {
        if (Corob is null)
        {
            return;
        }

        var choice = await _dialogs.ShowActionSheetAsync(
            "QR-код короба",
            "Отмена",
            "Сохранить в файл",
            "Поделиться");

        switch (choice)
        {
            case "Сохранить в файл":
                await SaveQrToFileAsync();
                break;
            case "Поделиться":
                await ShareQrAsync();
                break;
        }
    }

    private async Task SaveQrToFileAsync()
    {
        if (Corob is null)
        {
            return;
        }

        try
        {
            StatusMessage = "Генерация QR-кода...";
            var path = await _qrCodeService.GenerateAsync(Corob.Id, Corob.Name);
            var queueNote = await TryEnqueueQrAsync(path);

            StatusMessage = "Выберите папку для сохранения...";
            var saved = await _fileTransferService.SaveAsync(
                path,
                $"{SafeFileName(Corob.Name)}.png",
                "image/png");

            StatusMessage = saved
                ? "Файл QR сохранён." + queueNote
                : "Сохранение отменено." + queueNote;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
            await _dialogs.ShowAlertAsync("Сохранить в файл", StatusMessage);
        }
    }

    private async Task<string> TryEnqueueQrAsync(string path)
    {
        try
        {
            var name = $"{SafeFileName(Corob?.Name ?? "QR")}.png";
            await _transferQueue.EnqueueAsync(
                path,
                name,
                TransferKinds.Qr,
                Corob?.Id);
            return " Добавлен в очередь на компьютер.";
        }
        catch (Exception ex)
        {
            return $" Не удалось добавить в очередь: {ex.Message}";
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (safe.Length > 48)
        {
            safe = safe[..48].Trim();
        }

        return string.IsNullOrWhiteSpace(safe) ? "Короб" : safe;
    }

    private async Task DeleteBarcodeAsync(BoxBarcodeEntity? barcode)
    {
        if (barcode is null)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Удалить марку",
            $"Удалить марку {barcode.Barcode}?",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        if (barcode.Id != 0)
        {
            await _database.DeleteBarcodeAsync(barcode.Id);
        }

        Barcodes.Remove(barcode);
        UpdateCount();
        StatusMessage = "Марка удалена";
    }

    private void UpdateCount()
    {
        CountText = Corob is null
            ? "0"
            : Corob.Capacity > 0
                ? $"{Barcodes.Count} / {Corob.Capacity}"
                : RussianPlural.Marks(Barcodes.Count);
    }
}
