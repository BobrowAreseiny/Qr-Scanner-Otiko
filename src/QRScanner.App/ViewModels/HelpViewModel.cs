using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;

namespace QRScanner.App.ViewModels;

public sealed class HelpViewModel : ObservableObject
{
    public HelpViewModel(INavigationService navigation)
    {
        BackCommand = new AsyncRelayCommand(navigation.GoBackAsync);
    }

    public IAsyncRelayCommand BackCommand { get; }
}
