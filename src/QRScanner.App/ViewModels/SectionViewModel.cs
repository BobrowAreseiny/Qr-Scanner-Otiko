using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;

namespace QRScanner.App.ViewModels;

public sealed partial class SectionViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string sectionTitle = "Раздел";

    public SectionViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IAsyncRelayCommand BackCommand { get; }
}
