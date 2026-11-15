using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class OrderPage : ContentPage
{
    private readonly OrderViewModel _viewModel;
    private readonly IScannerService _scanner;
    private readonly IKeyboardShortcutService _keyboard;

    public OrderPage(
        OrderViewModel viewModel,
        IScannerService scanner,
        IKeyboardShortcutService keyboard)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _scanner = scanner;
        _keyboard = keyboard;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _scanner.BarcodeScanned += OnBarcodeScanned;
        _keyboard.ShortcutPressed -= OnShortcut;
        _keyboard.ShortcutPressed += OnShortcut;
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _keyboard.ShortcutPressed -= OnShortcut;
        base.OnDisappearing();
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
                _viewModel.SaveCommand.Execute(null);
                break;
            case 0:
                _viewModel.BackCommand.Execute(null);
                break;
        }
    }
}
