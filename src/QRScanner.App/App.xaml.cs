using Microsoft.Extensions.DependencyInjection;
using QRScanner.App.Data;
using QRScanner.App.Services;

namespace QRScanner.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
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
    }
}
