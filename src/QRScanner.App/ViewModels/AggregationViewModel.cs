using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed class AggregationViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public AggregationViewModel(INavigationService navigation)
    {
        _navigation = navigation;

        CreateCorobCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(CorobCreatePage)));

        OpenCorobsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(CorobListPage)));

        OpenInfoCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync($"{nameof(SectionPage)}?section={Uri.EscapeDataString("Информация")}"));

        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IAsyncRelayCommand CreateCorobCommand { get; }

    public IAsyncRelayCommand OpenCorobsCommand { get; }

    public IAsyncRelayCommand OpenInfoCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }
}
