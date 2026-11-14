using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services;
using QRScanner.App.Views;

namespace QRScanner.App.ViewModels;

public sealed partial class CorobCreateViewModel : ObservableObject
{
    private static readonly Color SelectedBackground = Color.FromArgb("#1E3A8A");
    private static readonly Color NormalBackground = Color.FromArgb("#E5E7EB");
    private static readonly Color SelectedText = Colors.White;
    private static readonly Color NormalText = Color.FromArgb("#111827");

    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    private bool _initialized;

    public CorobCreateViewModel(
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;

        SelectCapacityCommand = new Command<string>(SelectCapacity);
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private int capacity = 12;

    public ICommand SelectCapacityCommand { get; }

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    public Color Capacity12Background => Capacity == 12 ? SelectedBackground : NormalBackground;
    public Color Capacity10Background => Capacity == 10 ? SelectedBackground : NormalBackground;
    public Color Capacity5Background => Capacity == 5 ? SelectedBackground : NormalBackground;
    public Color Capacity3Background => Capacity == 3 ? SelectedBackground : NormalBackground;

    public Color Capacity12TextColor => Capacity == 12 ? SelectedText : NormalText;
    public Color Capacity10TextColor => Capacity == 10 ? SelectedText : NormalText;
    public Color Capacity5TextColor => Capacity == 5 ? SelectedText : NormalText;
    public Color Capacity3TextColor => Capacity == 3 ? SelectedText : NormalText;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        Name = $"№{await _database.GetNextCorobNumberAsync()}";
    }

    private void SelectCapacity(string? value)
    {
        if (int.TryParse(value, out var capacityValue) && capacityValue > 0)
        {
            Capacity = capacityValue;
        }
    }

    partial void OnCapacityChanged(int value)
    {
        OnPropertyChanged(nameof(Capacity12Background));
        OnPropertyChanged(nameof(Capacity10Background));
        OnPropertyChanged(nameof(Capacity5Background));
        OnPropertyChanged(nameof(Capacity3Background));
        OnPropertyChanged(nameof(Capacity12TextColor));
        OnPropertyChanged(nameof(Capacity10TextColor));
        OnPropertyChanged(nameof(Capacity5TextColor));
        OnPropertyChanged(nameof(Capacity3TextColor));
    }

    private async Task CreateAsync()
    {
        var trimmedName = Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            await _dialogs.ShowAlertAsync("Ошибка", "Введите название короба.");
            return;
        }

        try
        {
            var corob = await _database.SaveCorobAsync(new CorobEntity
            {
                Name = trimmedName,
                Capacity = Capacity,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _navigation.GoToAsync($"../{nameof(CorobDetailPage)}?id={corob.Id}");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAlertAsync("Ошибка", $"Не удалось создать короб: {ex.Message}");
        }
    }
}
