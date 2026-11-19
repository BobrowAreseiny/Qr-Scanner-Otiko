using Android.Content;
using Android.Views.InputMethods;
using Microsoft.Maui.ApplicationModel;
using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class SettingsSectionPage : ContentPage, IQueryAttributable
{
    private readonly SettingsViewModel _viewModel;
    private readonly IKeyboardShortcutService _keyboard;
    private readonly IScannerService _scanner;

    public SettingsSectionPage(
        SettingsViewModel viewModel,
        IKeyboardShortcutService keyboard,
        IScannerService scanner)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _keyboard = keyboard;
        _scanner = scanner;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("section", out var value) && value is string section)
        {
            _viewModel.ApplySection(Uri.UnescapeDataString(section));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _keyboard.ShortcutPressed -= OnShortcut;
        _keyboard.ShortcutPressed += OnShortcut;
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _scanner.BarcodeScanned += OnBarcodeScanned;
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        _keyboard.ShortcutPressed -= OnShortcut;
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _viewModel.Password = string.Empty;
        _viewModel.SmbPassword = string.Empty;
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
            await _viewModel.HandleSettingsScanAsync(args.Data);
        });
    }

    private void OnShortcut(int digit)
    {
        if (digit == 0)
        {
            _viewModel.BackCommand.Execute(null);
        }
    }
}
