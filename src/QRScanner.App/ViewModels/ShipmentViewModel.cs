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
        OpenInfoCommand = new AsyncRelayCommand(() => OpenSectionAsync("Информация"));
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IAsyncRelayCommand OpenOrderCommand { get; }

    public IAsyncRelayCommand OpenInfoCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    private Task OpenSectionAsync(string title)
    {
        var route = $"{nameof(SectionPage)}?section={Uri.EscapeDataString(title)}";
        return _navigation.GoToAsync(route);
    }
}
