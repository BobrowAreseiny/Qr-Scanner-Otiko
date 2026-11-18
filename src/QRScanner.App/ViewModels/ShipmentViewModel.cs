using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed partial class ShipmentViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly ITransferQueueService _transferQueue;

    public ShipmentViewModel(
        INavigationService navigation,
        ITransferQueueService transferQueue)
    {
        _navigation = navigation;
        _transferQueue = transferQueue;

        OpenOrderCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(OrderPage)));
        OpenHistoryCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(OrderHistoryPage)));
        SendCommand = new AsyncRelayCommand(SendAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IAsyncRelayCommand OpenOrderCommand { get; }

    public IAsyncRelayCommand OpenHistoryCommand { get; }

    public IAsyncRelayCommand SendCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    [ObservableProperty]
    private string pendingCountText = "Нет файлов для отправки";

    [ObservableProperty]
    private string sendButtonText = "Отправить на компьютер";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isSending;

    public async Task LoadAsync()
    {
        try
        {
            var count = await _transferQueue.CountPendingAsync();
            PendingCountText = FormatPending(count);
            SendButtonText = count == 0
                ? "Отправить на компьютер"
                : $"Отправить на компьютер ({count})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка очереди: {ex.Message}";
        }
    }

    private async Task SendAsync()
    {
        if (IsSending)
        {
            return;
        }

        IsSending = true;
        StatusMessage = "Отправка на компьютер...";

        try
        {
            var result = await _transferQueue.SendPendingAsync();
            StatusMessage = result.Message;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка отправки: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }

    private static string FormatPending(int count)
    {
        if (count <= 0)
        {
            return "Нет файлов для отправки";
        }

        var lastDigit = count % 10;
        var lastTwo = count % 100;

        if (lastDigit == 1 && lastTwo != 11)
        {
            return $"Ожидает {count} файл";
        }

        if (lastDigit is >= 2 and <= 4 && (lastTwo < 12 || lastTwo > 14))
        {
            return $"Ожидают {count} файла";
        }

        return $"Ожидают {count} файлов";
    }
}
