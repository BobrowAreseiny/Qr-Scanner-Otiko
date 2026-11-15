using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QRScanner.App.Data;
using QRScanner.App.Services;

namespace QRScanner.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ILogger<App> _logger;
    private readonly IApiSettingsService _apiSettings;
    private readonly IDatamarkApiClient _apiClient;

    public App(
        IServiceProvider services,
        ILogger<App> logger,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient)
    {
        InitializeComponent();
        _services = services;
        _logger = logger;
        _apiSettings = apiSettings;
        _apiClient = apiClient;

        _logger.LogInformation("Запуск приложения «Сканер QR»");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _ = InitializeDatabaseAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_services.GetRequiredService<AppShell>());
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _services.GetRequiredService<IDatabaseService>().InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации базы данных");
            System.Diagnostics.Debug.WriteLine($"Ошибка инициализации базы данных: {ex}");
            Console.WriteLine($"QRSCANNER_DB_ERROR: {ex}");
            return;
        }

        try
        {
            var created = await _services
                .GetRequiredService<IDatabaseBackupService>()
                .EnsureAutomaticBackupsAsync();

            Console.WriteLine($"QRSCANNER_BACKUP: created={created}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка резервного копирования базы данных: {ex}");
            Console.WriteLine($"QRSCANNER_BACKUP_ERROR: {ex.Message}");
        }

        await TryAutoLoginAsync();
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            var settings = await _apiSettings.LoadAsync();
            if (!settings.HasCredentials)
            {
                return;
            }

            var ok = await _apiClient.TestConnectionAsync(settings);
            _logger.LogInformation(
                "Автоматическая авторизация Datamark: {Result}",
                ok ? "успешно" : "не удалось");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось автоматически авторизоваться в Datamark");
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.LogCritical(
            e.ExceptionObject as Exception,
            "Необработанное исключение приложения");
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Необработанная ошибка задачи");
        e.SetObserved();
    }
}
