using Android.Content;
using Android.Views.InputMethods;
using Microsoft.Maui.ApplicationModel;
using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class OneCSettingsPage : ContentPage
{
    private readonly OneCSettingsViewModel _viewModel;
    private readonly IKeyboardShortcutService _keyboard;

    public OneCSettingsPage(
        OneCSettingsViewModel viewModel,
        IKeyboardShortcutService keyboard)
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
        HideKeyboard();
        base.OnDisappearing();
    }

    private static void HideKeyboard()
    {
        var activity = Platform.CurrentActivity;
        var focus = activity?.CurrentFocus;

        if (activity is null || focus is null)
        {
            return;
        }

        var manager = (InputMethodManager?)activity.GetSystemService(
            Context.InputMethodService);

        manager?.HideSoftInputFromWindow(focus.WindowToken, 0);
        focus.ClearFocus();
    }

    private void OnShortcut(int digit)
    {
        if (digit == 0)
        {
            _viewModel.BackCommand.Execute(null);
        }
    }
}
