using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed class CorobListItem
{
    public CorobListItem(CorobEntity corob, int barcodeCount)
    {
        Corob = corob;
        BarcodeCount = barcodeCount;
    }

    public CorobEntity Corob { get; }

    public int BarcodeCount { get; }

    public string Name => Corob.Name;

    public string CountText => Corob.Capacity > 0
        ? $"{BarcodeCount} / {Corob.Capacity}"
        : $"{BarcodeCount} марок";

    public string StatusText => Corob.IsShipped ? "Отгружен" : "Активен";
}

public sealed partial class CorobListViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public CorobListViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        OpenCommand = new AsyncRelayCommand<CorobListItem>(OpenAsync);
        DeleteCommand = new AsyncRelayCommand<CorobListItem>(DeleteAsync);
    }

    public ObservableCollection<CorobListItem> Corobs { get; } = new();

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand<CorobListItem> OpenCommand { get; }

    public IAsyncRelayCommand<CorobListItem> DeleteCommand { get; }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isEmpty;

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _database.GetCorobsAsync();
            var counts = await _database.GetBarcodeCountsByCorobAsync();
            Corobs.Clear();

            foreach (var corob in items)
            {
                Corobs.Add(new CorobListItem(
                    corob,
                    counts.GetValueOrDefault(corob.Id)));
            }

            IsEmpty = Corobs.Count == 0;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось загрузить короба: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CreateAsync()
    {
        return _navigation.GoToAsync(nameof(CorobCreatePage));
    }

    private Task OpenAsync(CorobListItem? item)
    {
        return item is null
            ? Task.CompletedTask
            : _navigation.GoToAsync($"{nameof(CorobDetailPage)}?id={item.Corob.Id}");
    }

    private async Task DeleteAsync(CorobListItem? item)
    {
        if (item is null)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Удалить короб",
            $"Удалить «{item.Name}» и все его марки?",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        await _database.DeleteCorobAsync(item.Corob.Id);
        Corobs.Remove(item);
        IsEmpty = Corobs.Count == 0;
    }
}
