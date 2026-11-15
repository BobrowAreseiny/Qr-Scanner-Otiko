using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed partial class OrderHistoryViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public OrderHistoryViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;

        OpenCommand = new AsyncRelayCommand<OrderHistoryItem>(OpenAsync);
        DeleteCommand = new AsyncRelayCommand<OrderHistoryItem>(DeleteAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public ObservableCollection<OrderHistoryItem> Orders { get; } = new();

    public IAsyncRelayCommand<OrderHistoryItem> OpenCommand { get; }

    public IAsyncRelayCommand<OrderHistoryItem> DeleteCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    [ObservableProperty]
    private bool isEmpty = true;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var orders = await _database.GetOrdersAsync();
            Orders.Clear();

            foreach (var order in orders)
            {
                var lines = await _database.GetOrderLinesAsync(order.Id);
                Orders.Add(new OrderHistoryItem
                {
                    OrderId = order.Id,
                    Name = order.Name,
                    DateText = order.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                    CountText = FormatCount(lines.Count),
                    StatusText = order.IsShipped ? "Отправлено" : "На складе",
                    OneCStatusText = order.IsSentTo1C
                        ? string.IsNullOrWhiteSpace(order.OneCNumber)
                            ? "1С: отправлен"
                            : $"1С: №{order.OneCNumber}"
                        : string.IsNullOrWhiteSpace(order.OneCError)
                            ? "1С: ожидает"
                            : "1С: ошибка"
                });
            }

            IsEmpty = Orders.Count == 0;
            StatusMessage = IsEmpty
                ? string.Empty
                : $"Всего заказов: {Orders.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task OpenAsync(OrderHistoryItem? item)
    {
        return item is null
            ? Task.CompletedTask
            : _navigation.GoToAsync($"{nameof(OrderHistoryDetailPage)}?id={item.OrderId}");
    }

    private async Task DeleteAsync(OrderHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Удалить заказ",
            $"Удалить заказ «{item.Name}»?\nКороба останутся на складе, статус «Отгружен» будет снят.",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var lines = await _database.GetOrderLinesAsync(item.OrderId);
            var corobIds = lines
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

                corob.IsShipped = false;
                await _database.SaveCorobAsync(corob);
            }

            await _database.DeleteOrderAsync(item.OrderId);
            Orders.Remove(item);
            IsEmpty = Orders.Count == 0;
            StatusMessage = IsEmpty
                ? string.Empty
                : $"Всего заказов: {Orders.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось удалить заказ: {ex.Message}");
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
