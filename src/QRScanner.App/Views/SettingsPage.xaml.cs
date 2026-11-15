using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly IKeyboardShortcutService _keyboard;

    public SettingsPage(SettingsViewModel viewModel, IKeyboardShortcutService keyboard)
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
        _viewModel.Password = string.Empty;
        base.OnDisappearing();
    }

    private void OnShortcut(int digit)
    {
        if (digit == 0)
        {
            _viewModel.BackCommand.Execute(null);
        }
    }
}
