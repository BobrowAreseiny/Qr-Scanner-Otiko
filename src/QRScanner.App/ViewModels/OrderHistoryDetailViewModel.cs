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

    private OrderEntity? _order;

    public OrderHistoryDetailViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;

        ShipCommand = new AsyncRelayCommand(() => SetShippedAsync(true));
        ReturnCommand = new AsyncRelayCommand(() => SetShippedAsync(false));
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public ObservableCollection<OrderLineEntity> Lines { get; } = new();

    public IAsyncRelayCommand ShipCommand { get; }

    public IAsyncRelayCommand ReturnCommand { get; }

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
