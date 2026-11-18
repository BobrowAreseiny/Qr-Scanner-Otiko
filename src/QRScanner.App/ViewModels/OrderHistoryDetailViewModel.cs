using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services;

namespace QRScanner.App.ViewModels;

public sealed partial class OrderHistoryDetailViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IOneCSettingsService _oneCSettings;
    private readonly IOneCApiClient _oneCClient;
    private readonly IOrderFileExporter _orderFileExporter;
    private readonly ITransferQueueService _transferQueue;

    private OrderEntity? _order;

    public OrderHistoryDetailViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs,
        IOneCSettingsService oneCSettings,
        IOneCApiClient oneCClient,
        IOrderFileExporter orderFileExporter,
        ITransferQueueService transferQueue)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;
        _oneCSettings = oneCSettings;
        _oneCClient = oneCClient;
        _orderFileExporter = orderFileExporter;
        _transferQueue = transferQueue;

        ShipCommand = new AsyncRelayCommand(() => SetShippedAsync(true));
        ReturnCommand = new AsyncRelayCommand(() => SetShippedAsync(false));
        SendTo1CCommand = new AsyncRelayCommand(SendTo1CAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public ObservableCollection<OrderLineEntity> Lines { get; } = new();

    public IAsyncRelayCommand ShipCommand { get; }

    public IAsyncRelayCommand ReturnCommand { get; }

    public IAsyncRelayCommand SendTo1CCommand { get; }

    public IAsyncRelayCommand ExportCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    [ObservableProperty]
    private string title = "Заказ";

    [ObservableProperty]
    private string dateText = string.Empty;

    [ObservableProperty]
    private string countText = "0 марок";

    [ObservableProperty]
    private string statusText = "На складе";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string oneCStatusText = "1С: не настроен";

    [ObservableProperty]
    private bool canSendTo1C;

    public async Task LoadAsync(int id)
    {
        var order = await _database.GetOrderAsync(id);
        if (order is null)
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Заказ не найден.");
            await _navigation.GoBackAsync();
            return;
        }

        _order = order;
        Title = order.Name;
        DateText = order.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        StatusText = order.IsShipped ? "Отправлено" : "На складе";

        Lines.Clear();
        var lines = await _database.GetOrderLinesAsync(order.Id);
        foreach (var line in lines)
        {
            Lines.Add(line);
        }

        CountText = FormatCount(Lines.Count);
        await RefreshOneCStatusAsync();
    }

    private async Task SendTo1CAsync()
    {
        if (_order is null)
        {
            return;
        }

        var settings = await _oneCSettings.LoadAsync();
        if (!settings.IsConfigured)
        {
            StatusMessage = "1С: API не настроен.";
            await _dialogs.ShowAlertAsync("1С", StatusMessage);
            return;
        }

        var wasSent = _order.IsSentTo1C;

        try
        {
            OneCOrderRequestFactory.EnsureExternalId(_order);
            await _database.SaveOrderAsync(_order);

            var request = OneCOrderRequestFactory.Create(_order, Lines, settings.Warehouse);
            var result = await _oneCClient.SendOrderAsync(settings, request);

            if (result.Success)
            {
                _order.IsSentTo1C = true;
                _order.SentTo1CAtUtc = DateTime.UtcNow;
                _order.OneCNumber = result.Number;
                _order.OneCError = null;
            }
            else
            {
                _order.IsSentTo1C = wasSent;
                _order.OneCError = result.Message;
            }

            await _database.SaveOrderAsync(_order);
            await RefreshOneCStatusAsync();
            StatusMessage = result.Message;
            await _dialogs.ShowAlertAsync("1С", result.Message);
        }
        catch (Exception ex)
        {
            _order.IsSentTo1C = wasSent;
            _order.OneCError = ex.Message;
            await _database.SaveOrderAsync(_order);
            await RefreshOneCStatusAsync();
            StatusMessage = $"Ошибка отправки в 1С: {ex.Message}";
            await _dialogs.ShowAlertAsync("1С", StatusMessage);
        }
    }

    private async Task RefreshOneCStatusAsync()
    {
        var settings = await _oneCSettings.LoadAsync();
        CanSendTo1C = settings.IsConfigured;

        if (!settings.IsConfigured)
        {
            OneCStatusText = "1С: не настроен";
            return;
        }

        if (_order is null)
        {
            OneCStatusText = string.Empty;
            return;
        }

        if (_order.IsSentTo1C)
        {
            OneCStatusText = string.IsNullOrWhiteSpace(_order.OneCNumber)
                ? "1С: отправлен"
                : $"1С: отправлен, №{_order.OneCNumber}";
            return;
        }

        OneCStatusText = string.IsNullOrWhiteSpace(_order.OneCError)
            ? "1С: ожидает отправки"
            : $"1С: ошибка — {_order.OneCError}";
    }

    private async Task ExportAsync()
    {
        if (_order is null || Lines.Count == 0)
        {
            await _dialogs.ShowAlertAsync("Экспорт", "В заказе нет марок.");
            return;
        }

        try
        {
            var export = await _orderFileExporter.ExportAsync(
                _order.Name,
                Lines.Select(line => line.Barcode));

            var queueNote = string.Empty;
            try
            {
                await _transferQueue.EnqueueAsync(
                    export.Location,
                    export.FileName,
                    TransferKinds.Order);
                queueNote = "\n\nДобавлено в очередь на компьютер.";
            }
            catch (Exception ex)
            {
                queueNote = $"\n\nНе удалось добавить в очередь: {ex.Message}";
            }

            StatusMessage = $"Файл сохранён: {export.DisplayPath}";
            await _dialogs.ShowAlertAsync(
                "Экспорт",
                export.IsPublic
                    ? $"Файл сохранён:\n{export.DisplayPath}{queueNote}"
                    : $"Файл сохранён во внутренней папке:\n{export.DisplayPath}{queueNote}",
                "OK");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
            await _dialogs.ShowAlertAsync(
                "Ошибка",
                $"Не удалось сохранить файл: {ex.Message}");
        }
    }

    private async Task SetShippedAsync(bool shipped)
    {
        if (_order is null)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
            shipped ? "Отправить заказ" : "Вернуть на склад",
            shipped
                ? "Отметить заказ как отправленный?"
                : "Вернуть короба заказа на склад?",
            shipped ? "Отправить" : "Вернуть",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        try
        {
            _order.IsShipped = shipped;
            await _database.SaveOrderAsync(_order);

            var corobIds = Lines
                .Where(line => line.SourceCorobId.HasValue)
                .Select(line => line.SourceCorobId!.Value)
                .Distinct();

            foreach (var corobId in corobIds)
            {
                var corob = await _database.GetCorobAsync(corobId);
                if (corob is null)
                {
                    continue;
                }

                corob.IsShipped = shipped;
                await _database.SaveCorobAsync(corob);
            }

            StatusText = shipped ? "Отправлено" : "На складе";
            StatusMessage = shipped
                ? "Заказ отмечен как отправленный."
                : "Заказ возвращён на склад.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось изменить статус заказа: {ex.Message}");
        }
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
