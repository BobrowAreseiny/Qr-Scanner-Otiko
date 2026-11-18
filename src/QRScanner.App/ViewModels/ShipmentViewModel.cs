using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed class ShipmentViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public ShipmentViewModel(INavigationService navigation)
    {
        _navigation = navigation;

        OpenOrderCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(OrderPage)));
        OpenHistoryCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(OrderHistoryPage)));
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IAsyncRelayCommand OpenOrderCommand { get; }

    public IAsyncRelayCommand OpenHistoryCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }
}
