using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly IKeyboardShortcutService _keyboard;

    public HomePage(HomeViewModel viewModel, IKeyboardShortcutService keyboard)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _keyboard = keyboard;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _keyboard.ShortcutPressed -= OnShortcut;
        _keyboard.ShortcutPressed += OnShortcut;
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        _keyboard.ShortcutPressed -= OnShortcut;
        base.OnDisappearing();
    }

    private void OnShortcut(int digit)
    {
        switch (digit)
        {
            case 1:
                _viewModel.OpenAggregationCommand.Execute(null);
                break;
            case 2:
                _viewModel.OpenShipmentCommand.Execute(null);
                break;
            case 3:
                _viewModel.SendToComputerCommand.Execute(null);
                break;
            case 4:
                _viewModel.OpenSettingsCommand.Execute(null);
                break;
            case 0:
                _viewModel.ExitCommand.Execute(null);
                break;
        }
    }
}
