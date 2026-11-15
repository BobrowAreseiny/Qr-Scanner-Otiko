using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;
using QRScanner.App.Services.Datamark;
using QRScanner.App.Views;
using QRScanner.Core.Barcodes;

namespace QRScanner.App.ViewModels;

public sealed partial class GtinCheckViewModel : ObservableObject
{
    private readonly IDatamarkApiClient _apiClient;
    private readonly IApiSettingsService _apiSettings;
    private readonly INavigationService _navigation;

    public GtinCheckViewModel(
        IDatamarkApiClient apiClient,
        IApiSettingsService apiSettings,
        INavigationService navigation)
    {
        _apiClient = apiClient;
        _apiSettings = apiSettings;
        _navigation = navigation;

        CheckCommand = new AsyncRelayCommand(CheckAsync);
        OpenSettingsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(SettingsPage)));
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IAsyncRelayCommand CheckCommand { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    [ObservableProperty]
    private string gtin = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Введите или отсканируйте GTIN.";

    [ObservableProperty]
    private bool isResultVisible;

    [ObservableProperty]
    private bool isAuthRequired;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string vendorCode = string.Empty;

    [ObservableProperty]
    private string size = string.Empty;

    [ObservableProperty]
    private string gtinCheckText = string.Empty;

    [ObservableProperty]
    private string groupText = string.Empty;

    [ObservableProperty]
    private string isMyText = string.Empty;

    public Task HandleBarcodeAsync(BarcodeScannedEventArgs args)
    {
        var code = BarcodeParser.Normalize(args.Data);
        if (code.Length == 0)
        {
            return Task.CompletedTask;
        }

        if (BarcodeParser.IsCorobCode(code))
        {
            StatusMessage = "Это код короба, а не GTIN или код маркировки.";
            return Task.CompletedTask;
        }

        Gtin = BarcodeParser.ExtractGtin(code) ?? code;
        return CheckAsync();
    }

    private async Task CheckAsync()
    {
        var normalized = GtinValidator.Normalize(Gtin);
        if (normalized is null || !GtinValidator.IsValid(normalized))
        {
            IsResultVisible = false;
            IsAuthRequired = false;
            StatusMessage = "Некорректный GTIN: проверьте 8, 12, 13 или 14 цифр и контрольную сумму.";
            return;
        }

        Gtin = normalized;

        var settings = await _apiSettings.LoadAsync();
        if (!settings.HasCredentials)
        {
            IsAuthRequired = true;
            IsResultVisible = false;
            StatusMessage = "Требуется авторизация. Войдите в настройках.";
            return;
        }

        IsAuthRequired = false;
        IsBusy = true;

        try
        {
            var product = await _apiClient.FindProductAsync(normalized, settings);
            if (product is null)
            {
                IsResultVisible = false;
                StatusMessage = $"GTIN {normalized} не найден в Электронном знаке.";
                return;
            }

            ProductName = product.Name ?? "Без названия";
            VendorCode = product.VendorCode ?? "—";
            Size = product.Size ?? "—";
            GtinCheckText = product.GtinCheck switch
            {
                true => "GTIN подтверждён",
                false => "GTIN не подтверждён",
                _ => "Нет данных"
            };
            GroupText = product.Group ?? "—";
            IsMyText = product.IsMy ? "Да" : "Нет";
            IsResultVisible = true;
            StatusMessage = "GTIN найден.";
        }
        catch (DatamarkApiException ex)
        {
            IsResultVisible = false;
            IsAuthRequired = ex.StatusCode is 401 or 403;
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            IsResultVisible = false;
            StatusMessage = $"Ошибка проверки GTIN: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
