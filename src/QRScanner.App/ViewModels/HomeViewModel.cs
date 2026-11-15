using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed class HomeViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public HomeViewModel(INavigationService navigation, IDialogService dialogs)
    {
        _navigation = navigation;
        _dialogs = dialogs;

        OpenAggregationCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(AggregationPage)));

        OpenShipmentCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(ShipmentPage)));

        ShowMenuCommand = new AsyncRelayCommand(ShowMenuAsync);
        ExitCommand = new AsyncRelayCommand(ExitAsync);
    }

    public IAsyncRelayCommand OpenAggregationCommand { get; }

    public IAsyncRelayCommand OpenShipmentCommand { get; }

    public IAsyncRelayCommand ShowMenuCommand { get; }

    public IAsyncRelayCommand ExitCommand { get; }

    private async Task ShowMenuAsync()
    {
        var choice = await _dialogs.ShowActionSheetAsync(
            "Меню",
            "Отмена",
            "Настройки",
            "Логи",
            "О программе",
            "Выход");

        switch (choice)
        {
            case "Настройки":
                await _navigation.GoToAsync(nameof(SettingsPage));
                break;
            case "Логи":
                await _navigation.GoToAsync(nameof(LogsPage));
                break;
            case "О программе":
                await ShowAboutAsync();
                break;
            case "Выход":
                await ExitAsync();
                break;
        }
    }

    private Task ShowAboutAsync()
    {
        return _dialogs.ShowAlertAsync(
            "О программе",
            "Сканер QR\nВерсия 1.0\n.NET 10 / MAUI",
            "OK");
    }

    private Task ExitAsync()
    {
        Application.Current?.Quit();
        return Task.CompletedTask;
    }
}
