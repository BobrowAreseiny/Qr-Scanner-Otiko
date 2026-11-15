using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services;
using QRScanner.App.Services.Datamark;
using QRScanner.Core.Barcodes;

namespace QRScanner.App.ViewModels;

public sealed partial class CorobDetailViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IApiSettingsService _apiSettings;
    private readonly IDatamarkApiClient _apiClient;

    public CorobDetailViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;
        _apiSettings = apiSettings;
        _apiClient = apiClient;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        DeleteBarcodeCommand = new AsyncRelayCommand<BoxBarcodeEntity>(DeleteBarcodeAsync);
        OpenSettingsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(QRScanner.App.Views.SettingsPage)));
    }

    public ObservableCollection<BoxBarcodeEntity> Barcodes { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand<BoxBarcodeEntity> DeleteBarcodeCommand { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }

    [ObservableProperty]
    private CorobEntity? corob;

    [ObservableProperty]
    private bool isAuthRequired;

    [ObservableProperty]
    private string title = "Короб";

    [ObservableProperty]
    private string countText = "0 / 0";

    [ObservableProperty]
    private string statusMessage = "Ожидание сканирования";

    public async Task LoadAsync(int id)
    {
        var corob = await _database.GetCorobAsync(id);
        if (corob is null)
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Короб не найден.");
            await _navigation.GoBackAsync();
            return;
        }

        Corob = corob;
        Title = corob.Name;
        Barcodes.Clear();

        var barcodes = await _database.GetBarcodesAsync(corob.Id);
        foreach (var barcode in barcodes)
        {
            Barcodes.Add(barcode);
        }

        UpdateCount();

        var settings = await _apiSettings.LoadAsync();
        IsAuthRequired = !settings.HasCredentials;
        if (IsAuthRequired)
        {
            StatusMessage = "Требуется авторизация Электронного знака. Отсканируйте марку после входа.";
        }
    }

    public async Task HandleBarcodeAsync(BarcodeScannedEventArgs args)
    {
        if (Corob is null)
        {
            return;
        }

        var code = BarcodeParser.Normalize(args.Data);
        if (code.Length == 0)
        {
            return;
        }

        if (BarcodeParser.IsCorobCode(code))
        {
            StatusMessage = "Это код короба, а не марка товара.";
            return;
        }

        var settings = await _apiSettings.LoadAsync();
        if (!settings.HasCredentials)
        {
            IsAuthRequired = true;
            StatusMessage = "Требуется авторизация Электронного знака. Укажите логин и пароль в настройках.";
            return;
        }

        IsAuthRequired = false;

        if (Barcodes.Any(b => b.Barcode == code))
        {
            StatusMessage = "Эта марка уже есть в коробе.";
            return;
        }

        if (await _database.FindBarcodeAsync(code) is not null)
        {
            StatusMessage = "Эта марка уже отсканирована в другом коробе.";
            return;
        }

        if (Barcodes.Count >= Corob.Capacity)
        {
            StatusMessage = $"Короб заполнен: {Barcodes.Count} из {Corob.Capacity}.";
            return;
        }

        var gtin = BarcodeParser.ExtractGtin(code);
        if (!string.IsNullOrWhiteSpace(gtin) && !GtinValidator.IsValid(gtin))
        {
            StatusMessage = "GTIN не прошёл проверку контрольной суммы.";
            return;
        }

        string? vendorCode = null;
        string? size = null;

        if (!string.IsNullOrWhiteSpace(gtin))
        {
            StatusMessage = "Запрос информации о товаре...";
            try
            {
                var product = await _apiClient.FindProductAsync(gtin!, settings);
                if (product is not null)
                {
                    vendorCode = product.VendorCode;
                    size = product.Size;
                }
            }
            catch (DatamarkApiException ex) when (ex.StatusCode is 401 or 403)
            {
                IsAuthRequired = true;
                StatusMessage = "Требуется авторизация Электронного знака. Проверьте логин и пароль в настройках.";
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Электронный знак недоступен: {ex.Message}";
            }
        }

        Barcodes.Add(new BoxBarcodeEntity
        {
            CorobId = Corob.Id,
            Barcode = code,
            Gtin = gtin,
            VendorCode = vendorCode,
            Size = size,
            ScannedAtUtc = DateTime.UtcNow
        });

        UpdateCount();
        StatusMessage = vendorCode is null && size is null
            ? "Марка добавлена. Информация о товаре не получена."
            : $"Товар: {vendorCode} {size}".Trim();
    }

    private async Task SaveAsync()
    {
        if (Corob is null)
        {
            return;
        }

        var name = Corob.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Введите название короба.");
            return;
        }

        try
        {
            Corob.Name = name;
            await _database.SaveCorobAsync(Corob);

            foreach (var barcode in Barcodes.Where(b => b.Id == 0))
            {
                barcode.CorobId = Corob.Id;
                await _database.SaveBarcodeAsync(barcode);
            }

            StatusMessage = "Сохранено";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось сохранить короб: {ex.Message}");
        }
    }

    private async Task DeleteBarcodeAsync(BoxBarcodeEntity? barcode)
    {
        if (barcode is null)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Удалить марку",
            $"Удалить марку {barcode.Barcode}?",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        if (barcode.Id != 0)
        {
            await _database.DeleteBarcodeAsync(barcode.Id);
        }

        Barcodes.Remove(barcode);
        UpdateCount();
        StatusMessage = "Марка удалена";
    }

    private void UpdateCount()
    {
        CountText = $"{Barcodes.Count} / {Corob?.Capacity ?? 0}";
    }
}
