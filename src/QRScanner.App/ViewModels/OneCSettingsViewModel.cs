using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Services;
using QRScanner.Core.Integration;

namespace QRScanner.App.ViewModels;

public sealed partial class OneCSettingsViewModel : ObservableObject
{
    private const string AuthNone = "Без авторизации";
    private const string AuthBearer = "Bearer-токен";
    private const string AuthBasic = "Basic (логин и пароль)";

    private readonly IOneCSettingsService _settingsService;
    private readonly IOneCApiClient _apiClient;
    private readonly IDatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public OneCSettingsViewModel(
        IOneCSettingsService settingsService,
        IOneCApiClient apiClient,
        IDatabaseService database,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _settingsService = settingsService;
        _apiClient = apiClient;
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestCommand = new AsyncRelayCommand(TestAsync);
        SendPendingCommand = new AsyncRelayCommand(SendPendingAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public IReadOnlyList<string> AuthTypes { get; } =
        [AuthNone, AuthBearer, AuthBasic];

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand TestCommand { get; }

    public IAsyncRelayCommand SendPendingCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private string ordersPath = OneCConnectionSettings.DefaultOrdersPath;

    [ObservableProperty]
    private string healthPath = OneCConnectionSettings.DefaultHealthPath;

    [ObservableProperty]
    private string selectedAuthType = AuthNone;

    [ObservableProperty]
    private string token = string.Empty;

    [ObservableProperty]
    private string login = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isPasswordVisible;

    public bool IsPasswordHidden => !IsPasswordVisible;

    partial void OnIsPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPasswordHidden));
    }

    [ObservableProperty]
    private string warehouse = OneCConnectionSettings.DefaultWarehouse;

    [ObservableProperty]
    private string timeoutSeconds = OneCConnectionSettings.DefaultTimeoutSeconds
        .ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool allowInvalidCertificate;

    [ObservableProperty]
    private bool isBearerAuth;

    [ObservableProperty]
    private bool isBasicAuth;

    [ObservableProperty]
    private string statusMessage = "1С не настроена.";

    [ObservableProperty]
    private bool isBusy;

    public async Task LoadAsync()
    {
        var settings = await _settingsService.LoadAsync();

        BaseUrl = settings.BaseUrl;
        OrdersPath = settings.OrdersPath;
        HealthPath = settings.HealthPath;
        Token = settings.Token;
        Login = settings.Login;
        Password = settings.Password;
        IsPasswordVisible = false;
        Warehouse = settings.Warehouse;
        TimeoutSeconds = settings.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        AllowInvalidCertificate = settings.AllowInvalidCertificate;

        SelectedAuthType = settings.AuthType switch
        {
            OneCAuthType.Bearer => AuthBearer,
            OneCAuthType.Basic => AuthBasic,
            _ => AuthNone
        };

        UpdateAuthVisibility();
        StatusMessage = settings.IsConfigured
            ? "Настройки 1С загружены."
            : "1С не настроена.";
    }

    partial void OnSelectedAuthTypeChanged(string value)
    {
        UpdateAuthVisibility();
    }

    private void UpdateAuthVisibility()
    {
        IsBearerAuth = SelectedAuthType == AuthBearer;
        IsBasicAuth = SelectedAuthType == AuthBasic;
    }

    private async Task SaveAsync()
    {
        var settings = BuildSettings();
        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        try
        {
            await _settingsService.SaveAsync(settings);
            StatusMessage = "Настройки 1С сохранены.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private async Task TestAsync()
    {
        var settings = BuildSettings();
        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiClient.TestConnectionAsync(settings);
            StatusMessage = result.Message;
            await _dialogs.ShowAlertAsync("1С", result.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка проверки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SendPendingAsync()
    {
        var settings = BuildSettings();
        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        try
        {
            await _settingsService.SaveAsync(settings);

            var orders = await _database.GetOrdersAsync();
            var pending = orders.Where(order => !order.IsSentTo1C).ToList();

            if (pending.Count == 0)
            {
                StatusMessage = "Нет заказов, ожидающих отправки.";
                return;
            }

            IsBusy = true;
            var sent = 0;
            var failed = 0;
            string? firstError = null;

            foreach (var order in pending)
            {
                OneCOrderRequestFactory.EnsureExternalId(order);
                await _database.SaveOrderAsync(order);

                var lines = await _database.GetOrderLinesAsync(order.Id);
                if (lines.Count == 0)
                {
                    order.IsSentTo1C = false;
                    order.OneCError = "В заказе нет строк.";
                    await _database.SaveOrderAsync(order);
                    failed++;
                    firstError ??= "В заказе нет строк.";
                    continue;
                }

                var request = OneCOrderRequestFactory.Create(order, lines, settings.Warehouse);
                var result = await _apiClient.SendOrderAsync(settings, request);

                if (result.Success)
                {
                    order.IsSentTo1C = true;
                    order.SentTo1CAtUtc = DateTime.UtcNow;
                    order.OneCNumber = result.Number;
                    order.OneCError = null;
                    sent++;
                }
                else
                {
                    order.IsSentTo1C = false;
                    order.OneCError = result.Message;
                    failed++;
                    firstError ??= result.Message;
                }

                await _database.SaveOrderAsync(order);
            }

            StatusMessage = $"Отправлено: {sent}. Ошибок: {failed}." +
                            (firstError is null ? string.Empty : $" Первая ошибка: {firstError}");

            await _dialogs.ShowAlertAsync("1С", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка отправки: {ex.Message}";
            await _dialogs.ShowAlertAsync("1С", StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private OneCConnectionSettings BuildSettings()
    {
        var timeout = int.TryParse(TimeoutSeconds, out var value)
            ? value
            : OneCConnectionSettings.DefaultTimeoutSeconds;

        var authType = SelectedAuthType switch
        {
            AuthBearer => OneCAuthType.Bearer,
            AuthBasic => OneCAuthType.Basic,
            _ => OneCAuthType.None
        };

        return new OneCConnectionSettings
        {
            BaseUrl = BaseUrl,
            OrdersPath = OrdersPath,
            HealthPath = HealthPath,
            AuthType = authType,
            Token = Token,
            Login = Login,
            Password = Password,
            Warehouse = Warehouse,
            TimeoutSeconds = timeout,
            AllowInvalidCertificate = AllowInvalidCertificate
        }.Normalize();
    }
}
