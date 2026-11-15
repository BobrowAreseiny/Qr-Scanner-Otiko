using QRScanner.App.Services;
using QRScanner.App.ViewModels;

namespace QRScanner.App.Views;

public partial class OrderHistoryDetailPage : ContentPage, IQueryAttributable
{
    private readonly OrderHistoryDetailViewModel _viewModel;
    private readonly IKeyboardShortcutService _keyboard;

    private int _orderId;

    public OrderHistoryDetailPage(
        OrderHistoryDetailViewModel viewModel,
        IKeyboardShortcutService keyboard)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _keyboard = keyboard;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) &&
            int.TryParse(value?.ToString(), out var id))
        {
            _orderId = id;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _keyboard.ShortcutPressed -= OnShortcut;
        _keyboard.ShortcutPressed += OnShortcut;

        if (_orderId > 0)
        {
            await _viewModel.LoadAsync(_orderId);
        }
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
                _viewModel.ShipCommand.Execute(null);
                break;
            case 2:
                _viewModel.ReturnCommand.Execute(null);
                break;
            case 3:
                _viewModel.SendTo1CCommand.Execute(null);
                break;
            case 0:
                _viewModel.BackCommand.Execute(null);
                break;
        }
    }
}
