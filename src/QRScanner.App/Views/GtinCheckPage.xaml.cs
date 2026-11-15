using Android.Content;
using Android.Views.InputMethods;
using Microsoft.Maui.ApplicationModel;
using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class GtinCheckPage : ContentPage
{
    private readonly GtinCheckViewModel _viewModel;
    private readonly IScannerService _scanner;
    private readonly IKeyboardShortcutService _keyboard;

    public GtinCheckPage(
        GtinCheckViewModel viewModel,
        IScannerService scanner,
        IKeyboardShortcutService keyboard)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _scanner = scanner;
        _keyboard = keyboard;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _scanner.BarcodeScanned += OnBarcodeScanned;
        _keyboard.ShortcutPressed -= OnShortcut;
        _keyboard.ShortcutPressed += OnShortcut;
    }

    protected override void OnDisappearing()
    {
        _scanner.BarcodeScanned -= OnBarcodeScanned;
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

    private void OnBarcodeScanned(object? sender, BarcodeScannedEventArgs args)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _viewModel.HandleBarcodeAsync(args);
        });
    }

    private void OnShortcut(int digit)
    {
        switch (digit)
        {
            case 1:
                _viewModel.CheckCommand.Execute(null);
                break;
            case 2:
                _viewModel.OpenSettingsCommand.Execute(null);
                break;
            case 0:
                _viewModel.BackCommand.Execute(null);
                break;
        }
    }
}
