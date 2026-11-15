using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
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
    private readonly IQrCodeService _qrCodeService;
    private readonly IFileTransferService _fileTransferService;

    public CorobDetailViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient,
        IQrCodeService qrCodeService,
        IFileTransferService fileTransferService)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;
        _apiSettings = apiSettings;
        _apiClient = apiClient;
        _qrCodeService = qrCodeService;
        _fileTransferService = fileTransferService;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        DeleteBarcodeCommand = new AsyncRelayCommand<BoxBarcodeEntity>(DeleteBarcodeAsync);
        OpenSettingsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(QRScanner.App.Views.SettingsPage)));
        ShareQrCommand = new AsyncRelayCommand(ShareQrAsync);
        SaveQrToFileCommand = new AsyncRelayCommand(SaveQrToFileAsync);
        QrMenuCommand = new AsyncRelayCommand(ShowQrMenuAsync);
    }

    public ObservableCollection<BoxBarcodeEntity> Barcodes { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand<BoxBarcodeEntity> DeleteBarcodeCommand { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }

    public IAsyncRelayCommand ShareQrCommand { get; }

    public IAsyncRelayCommand SaveQrToFileCommand { get; }

    public IAsyncRelayCommand QrMenuCommand { get; }

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

        if (Corob.Capacity > 0 && Barcodes.Count >= Corob.Capacity)
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

    private async Task ShareQrAsync()
    {
        if (Corob is null)
        {
            return;
        }

        try
        {
            StatusMessage = "Генерация QR-кода...";
            var path = await _qrCodeService.GenerateAsync(Corob.Id, Corob.Name);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"QR-код короба {Corob.Name}",
                File = new ShareFile(path)
            });

            StatusMessage = "QR-код готов.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка QR: {ex.Message}";
            await _dialogs.ShowAlertAsync("QR-код", StatusMessage);
        }
    }

    private async Task ShowQrMenuAsync()
    {
        if (Corob is null)
        {
            return;
        }

        var choice = await _dialogs.ShowActionSheetAsync(
            "QR-код короба",
            "Отмена",
            "Сохранить в файл",
            "Поделиться");

        switch (choice)
        {
            case "Сохранить в файл":
                await SaveQrToFileAsync();
                break;
            case "Поделиться":
                await ShareQrAsync();
                break;
        }
    }

    private async Task SaveQrToFileAsync()
    {
        if (Corob is null)
        {
            return;
        }

        try
        {
            StatusMessage = "Генерация QR-кода...";
            var path = await _qrCodeService.GenerateAsync(Corob.Id, Corob.Name);

            StatusMessage = "Выберите папку для сохранения...";
            var saved = await _fileTransferService.SaveAsync(
                path,
                $"{SafeFileName(Corob.Name)}.png",
                "image/png");

            StatusMessage = saved
                ? "Файл QR сохранён."
                : "Сохранение отменено.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
            await _dialogs.ShowAlertAsync("Сохранить в файл", StatusMessage);
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Короб" : safe;
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
        CountText = Corob is null
            ? "0"
            : Corob.Capacity > 0
                ? $"{Barcodes.Count} / {Corob.Capacity}"
                : $"{Barcodes.Count} марок";
    }
}
