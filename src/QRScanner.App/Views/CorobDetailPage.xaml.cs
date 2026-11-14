using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class CorobDetailPage : ContentPage, IQueryAttributable
{
    private readonly CorobDetailViewModel _viewModel;
    private readonly IScannerService _scanner;
    private readonly IKeyboardShortcutService _keyboard;

    private int _corobId;

    public CorobDetailPage(
        CorobDetailViewModel viewModel,
        IScannerService scanner,
        IKeyboardShortcutService keyboard)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _scanner = scanner;
        _keyboard = keyboard;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) &&
            int.TryParse(value?.ToString(), out var id))
        {
            _corobId = id;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _scanner.BarcodeScanned += OnBarcodeScanned;
        _keyboard.ShortcutPressed -= OnShortcut;
        _keyboard.ShortcutPressed += OnShortcut;

        if (_corobId > 0)
        {
            await _viewModel.LoadAsync(_corobId);
        }
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
            case 2:
                _viewModel.QrMenuCommand.Execute(null);
                break;
            case 0:
                _viewModel.BackCommand.Execute(null);
                break;
        }
    }
}
