using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ITransferQueueService _transferQueue;

    public HomeViewModel(
        INavigationService navigation,
        IDialogService dialogs,
        ITransferQueueService transferQueue)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _transferQueue = transferQueue;
        _transferQueue.PendingChanged += OnPendingChanged;

        OpenAggregationCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(AggregationPage)));

        OpenShipmentCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(ShipmentPage)));

        OpenSettingsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(SettingsPage)));

        OpenHelpCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(HelpPage)));

        SendToComputerCommand = new AsyncRelayCommand(SendToComputerAsync);
        ShowMenuCommand = new AsyncRelayCommand(ShowMenuAsync);
        ExitCommand = new AsyncRelayCommand(ExitAsync);
    }

    public IAsyncRelayCommand OpenAggregationCommand { get; }

    public IAsyncRelayCommand OpenShipmentCommand { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }

    public IAsyncRelayCommand OpenHelpCommand { get; }

    public IAsyncRelayCommand SendToComputerCommand { get; }

    public IAsyncRelayCommand ShowMenuCommand { get; }

    public IAsyncRelayCommand ExitCommand { get; }

    [ObservableProperty]
    private string pendingCountText = "Нет файлов";

    [ObservableProperty]
    private string sendStatusMessage = "QR всех коробов проверяются автоматически.";

    [ObservableProperty]
    private bool isSending;

    public string SendTileText => IsSending ? "Отправка..." : PendingCountText;

    partial void OnIsSendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SendTileText));
    }

    partial void OnPendingCountTextChanged(string value)
    {
        OnPropertyChanged(nameof(SendTileText));
    }

    public async Task LoadAsync()
    {
        try
        {
            var count = await _transferQueue.CountPendingAsync();
            PendingCountText = FormatPending(count);
        }
        catch (Exception ex)
        {
            SendStatusMessage = $"Ошибка очереди: {ex.Message}";
        }
    }

    private void OnPendingChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    private async Task SendToComputerAsync()
    {
        if (IsSending)
        {
            return;
        }

        IsSending = true;
        SendStatusMessage = "Отправка на компьютер...";

        try
        {
            var result = await _transferQueue.SendPendingAsync();
            SendStatusMessage = result.Message;
            await LoadAsync();
            await _dialogs.ShowAlertAsync("Компьютер", result.Message, "OK");
        }
        catch (Exception ex)
        {
            SendStatusMessage = $"Ошибка отправки: {ex.Message}";
            await _dialogs.ShowAlertAsync("Компьютер", SendStatusMessage, "OK");
        }
        finally
        {
            IsSending = false;
        }
    }

    private async Task ShowMenuAsync()
    {
        var choice = await _dialogs.ShowActionSheetAsync(
            "Меню",
            "Отмена",
            "Логи",
            "О программе");

        switch (choice)
        {
            case "Логи":
                await _navigation.GoToAsync(nameof(LogsPage));
                break;
            case "О программе":
                await ShowAboutAsync();
                break;
        }
    }

    private Task ShowAboutAsync()
    {
        return _dialogs.ShowAlertAsync(
            "О программе",
            $"Сканер QR\nВерсия {AppInfo.Current.VersionString}\n.NET 10 / MAUI",
            "OK");
    }

    private Task ExitAsync()
    {
        Application.Current?.Quit();
        return Task.CompletedTask;
    }

    private static string FormatPending(int count)
    {
        if (count <= 0)
        {
            return "Нет файлов";
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
