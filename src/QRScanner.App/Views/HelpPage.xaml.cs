using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class HelpPage : ContentPage
{
    private readonly HelpViewModel _viewModel;
    private readonly IKeyboardShortcutService _keyboard;

    public HelpPage(HelpViewModel viewModel, IKeyboardShortcutService keyboard)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _keyboard = keyboard;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _keyboard.ShortcutPressed -= OnShortcut;
        _keyboard.ShortcutPressed += OnShortcut;
    }

    protected override void OnDisappearing()
    {
        _keyboard.ShortcutPressed -= OnShortcut;
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
