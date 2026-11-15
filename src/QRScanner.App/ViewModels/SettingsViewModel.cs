using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Services;

namespace QRScanner.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IApiSettingsService _apiSettings;
    private readonly IDatamarkApiClient _apiClient;

    public SettingsViewModel(
        INavigationService navigation,
        IDialogService dialogs,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _apiSettings = apiSettings;
        _apiClient = apiClient;

        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestCommand = new AsyncRelayCommand(TestAsync);
        ShowAboutCommand = new AsyncRelayCommand(ShowAboutAsync);
    }

    [ObservableProperty]
    private string server = string.Empty;

    [ObservableProperty]
    private string login = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand TestCommand { get; }

    public IAsyncRelayCommand ShowAboutCommand { get; }

    public async Task LoadAsync()
    {
        var settings = await _apiSettings.LoadAsync();
        Server = settings.BaseUrl;
        Login = settings.Login;
        Password = settings.Password;
        StatusMessage = settings.HasCredentials
            ? "Данные Электронного знака (Datamark) загружены."
            : "Укажите логин и пароль Электронного знака.";
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await _apiSettings.SaveAsync(new ApiSettings(Server, Login, Password));
            StatusMessage = "Настройки Электронного знака сохранены.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestAsync()
    {
        IsBusy = true;
        StatusMessage = "Проверка подключения...";

        try
        {
            var settings = new ApiSettings(Server, Login, Password);
            await _apiSettings.SaveAsync(settings);

            var ok = await _apiClient.TestConnectionAsync(settings);
            StatusMessage = ok
                ? "Подключение к Электронному знаку (Datamark) установлено."
                : "Не удалось подключиться к Электронному знаку (Datamark).";
            await _dialogs.ShowAlertAsync("Электронный знак", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подключения: {ex.Message}";
            await _dialogs.ShowAlertAsync("Электронный знак", StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ShowAboutAsync()
    {
        return _dialogs.ShowAlertAsync(
            "О программе",
            "Сканер QR\nВерсия 1.0\n.NET 10 / MAUI",
            "OK");
    }
}
