using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services;
using QRScanner.Core.Barcodes;

namespace QRScanner.App.ViewModels;

public sealed partial class CorobInfoViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    private BoxBarcodeEntity? _barcode;
    private CorobEntity? _corob;

    public CorobInfoViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;

        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IAsyncRelayCommand DeleteCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    [ObservableProperty]
    private string statusMessage = "Ожидание сканирования";

    [ObservableProperty]
    private bool hasResult;

    [ObservableProperty]
    private string corobTitle = string.Empty;

    [ObservableProperty]
    private string corobMeta = string.Empty;

    [ObservableProperty]
    private string barcodeText = string.Empty;

    [ObservableProperty]
    private string productText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public async Task HandleBarcodeAsync(BarcodeScannedEventArgs args)
    {
        if (IsBusy)
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
            ClearResult();
            StatusMessage = "Это код короба. Отсканируйте марку товара.";
            return;
        }

        IsBusy = true;
        try
        {
            var barcode = await _database.FindBarcodeAsync(code);
            if (barcode is null)
            {
                ClearResult();
                StatusMessage = $"Марка {code} не найдена.";
                return;
            }

            var corob = await _database.GetCorobAsync(barcode.CorobId);
            if (corob is null)
            {
                ClearResult();
                StatusMessage = "Короб для найденной марки отсутствует.";
                return;
            }

            var barcodes = await _database.GetBarcodesAsync(corob.Id);

            _barcode = barcode;
            _corob = corob;

            CorobTitle = corob.Name;
            CorobMeta = BuildCorobMeta(barcodes.Count, corob);
            BarcodeText = BarcodeParser.CleanForExport(barcode.Barcode);
            ProductText = BuildProductText(barcode);
            HasResult = true;
            StatusMessage = "Марка найдена.";
        }
        catch (Exception ex)
        {
            ClearResult();
            StatusMessage = $"Ошибка поиска: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_barcode is null || _corob is null)
        {
            return;
        }

        var barcode = _barcode;
        var corob = _corob;

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Удалить марку",
            $"Удалить марку {barcode.DisplayTitle} из короба {corob.Name}?",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        try
        {
            if (barcode.Id != 0)
            {
                await _database.DeleteBarcodeAsync(barcode.Id);
            }

            var remaining = await _database.GetBarcodesAsync(corob.Id);

            _barcode = null;
            _corob = null;
            ClearResult();
            StatusMessage = $"Марка удалена. В коробе {corob.Name}: {RussianPlural.Marks(remaining.Count)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось удалить марку: {ex.Message}");
        }
    }

    private void ClearResult()
    {
        HasResult = false;
        CorobTitle = string.Empty;
        CorobMeta = string.Empty;
        BarcodeText = string.Empty;
        ProductText = string.Empty;
    }

    private static string BuildCorobMeta(int count, CorobEntity corob)
    {
        var meta = corob.Capacity > 0
            ? $"{count} / {corob.Capacity}"
            : RussianPlural.Marks(count);
        return corob.IsShipped ? $"{meta} · Отгружен" : meta;
    }

    private static string BuildProductText(BoxBarcodeEntity barcode)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(barcode.ProductName) ? null : barcode.ProductName,
            string.IsNullOrWhiteSpace(barcode.VendorCode) ? null : $"Артикул: {barcode.VendorCode}",
            string.IsNullOrWhiteSpace(barcode.Size) ? null : $"Размер: {barcode.Size}",
            string.IsNullOrWhiteSpace(barcode.Gtin) ? null : $"GTIN: {barcode.Gtin}",
            string.IsNullOrWhiteSpace(barcode.ProductGroup) ? null : $"Группа: {barcode.ProductGroup}",
            barcode.IsMyProduct.HasValue
                ? (barcode.IsMyProduct.Value ? "Наш товар: Да" : "Наш товар: Нет")
                : null
        };

        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(text) ? "Информация о товаре отсутствует" : text;
    }

}
