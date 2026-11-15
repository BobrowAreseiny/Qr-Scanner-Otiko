using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using CommunityToolkit.Mvvm.Input;
using QRScanner.App.Data;
using QRScanner.App.Services;
using QRScanner.App.Views;
using QRScanner.Core.Backup;

namespace QRScanner.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IApiSettingsService _apiSettings;
    private readonly IDatamarkApiClient _apiClient;
    private readonly IDbKeyProvider _keyProvider;
    private readonly IDatabaseBackupService _backupService;
    private readonly IFileTransferService _fileTransfer;
    private readonly IDatabaseService _database;

    public SettingsViewModel(
        INavigationService navigation,
        IDialogService dialogs,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient,
        IDbKeyProvider keyProvider,
        IDatabaseBackupService backupService,
        IFileTransferService fileTransfer,
        IDatabaseService database)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _apiSettings = apiSettings;
        _apiClient = apiClient;
        _keyProvider = keyProvider;
        _backupService = backupService;
        _fileTransfer = fileTransfer;
        _database = database;

        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestCommand = new AsyncRelayCommand(TestAsync);
        ShowAboutCommand = new AsyncRelayCommand(ShowAboutAsync);
        OpenOneCSettingsCommand = new AsyncRelayCommand(
            () => _navigation.GoToAsync(nameof(OneCSettingsPage)));
        ShowRecoveryCodeCommand = new AsyncRelayCommand(ShowRecoveryCodeAsync);
        CopyRecoveryCodeCommand = new AsyncRelayCommand(CopyRecoveryCodeAsync);
        SaveRecoveryCodeCommand = new AsyncRelayCommand(SaveRecoveryCodeAsync);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
        SaveBackupCommand = new AsyncRelayCommand(SaveBackupAsync);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);
        ImportLegacyDatabaseCommand = new AsyncRelayCommand(ImportLegacyDatabaseAsync);
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

    [ObservableProperty]
    private string recoveryCode = string.Empty;

    [ObservableProperty]
    private bool isRecoveryCodeVisible;

    [ObservableProperty]
    private string recoveryStatusMessage = string.Empty;

    [ObservableProperty]
    private string backupStatusMessage = string.Empty;

    [ObservableProperty]
    private string lastBackupText = "Копий нет.";

    [ObservableProperty]
    private string legacyImportStatusMessage = string.Empty;

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand TestCommand { get; }

    public IAsyncRelayCommand ShowAboutCommand { get; }

    public IAsyncRelayCommand OpenOneCSettingsCommand { get; }

    public IAsyncRelayCommand ShowRecoveryCodeCommand { get; }

    public IAsyncRelayCommand CopyRecoveryCodeCommand { get; }

    public IAsyncRelayCommand SaveRecoveryCodeCommand { get; }

    public IAsyncRelayCommand CreateBackupCommand { get; }

    public IAsyncRelayCommand SaveBackupCommand { get; }

    public IAsyncRelayCommand RestoreBackupCommand { get; }

    public IAsyncRelayCommand ImportLegacyDatabaseCommand { get; }

    public async Task LoadAsync()
    {
        var settings = await _apiSettings.LoadAsync();
        Server = settings.BaseUrl;
        Login = settings.Login;
        Password = settings.Password;
        StatusMessage = settings.HasCredentials
            ? "Данные Электронного знака (Datamark) загружены."
            : "Укажите логин и пароль Электронного знака.";

        await RefreshBackupsAsync();
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

    private async Task ShowRecoveryCodeAsync()
    {
        try
        {
            await GetRecoveryCodeAsync();
            IsRecoveryCodeVisible = true;
            RecoveryStatusMessage = "Код восстановления показан.";
        }
        catch (Exception ex)
        {
            RecoveryStatusMessage = $"Ошибка чтения кода: {ex.Message}";
        }
    }

    private async Task CopyRecoveryCodeAsync()
    {
        try
        {
            var code = await GetRecoveryCodeAsync();
            await Clipboard.Default.SetTextAsync(code);
            IsRecoveryCodeVisible = true;
            RecoveryStatusMessage = "Код скопирован в буфер обмена.";
        }
        catch (Exception ex)
        {
            RecoveryStatusMessage = $"Ошибка копирования: {ex.Message}";
        }
    }

    private async Task SaveRecoveryCodeAsync()
    {
        try
        {
            var code = await GetRecoveryCodeAsync();
            var directory = Path.Combine(FileSystem.CacheDirectory, "recovery");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "recovery-code.txt");
            var text = "Код восстановления базы данных «Сканер QR»\n" +
                       code + "\n\n" +
                       "Без этого кода восстановление зашифрованной базы невозможно.\n";

            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));

            var saved = await _fileTransfer.SaveAsync(
                path,
                "Код восстановления QR.txt",
                "text/plain");

            RecoveryStatusMessage = saved
                ? "Файл с кодом сохранён."
                : "Сохранение отменено.";
        }
        catch (Exception ex)
        {
            RecoveryStatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private async Task CreateBackupAsync()
    {
        IsBusy = true;
        try
        {
            var info = await _backupService.CreateBackupAsync(BackupKind.Manual);
            await RefreshBackupsAsync();
            BackupStatusMessage = $"Копия создана: {FormatSize(info.SizeBytes)}.";
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Ошибка создания копии: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveBackupAsync()
    {
        try
        {
            var latest = await _backupService.GetLatestBackupAsync();
            if (latest is null)
            {
                BackupStatusMessage = "Сначала создайте копию базы.";
                return;
            }

            var saved = await _fileTransfer.SaveAsync(
                latest.Path,
                Path.GetFileName(latest.Path),
                "application/octet-stream");

            BackupStatusMessage = saved
                ? "Копия сохранена в выбранную папку."
                : "Сохранение отменено.";
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private async Task RestoreBackupAsync()
    {
        string? pickedPath = null;
        try
        {
            pickedPath = await _fileTransfer.PickAsync("application/octet-stream");
            if (pickedPath is null)
            {
                BackupStatusMessage = "Файл не выбран.";
                return;
            }

            var confirmed = await _dialogs.ShowConfirmationAsync(
                "Восстановление базы",
                "Текущие данные будут заменены данными из копии. Продолжить?",
                "Продолжить",
                "Отмена");

            if (!confirmed)
            {
                BackupStatusMessage = "Восстановление отменено.";
                return;
            }

            var key = await _dialogs.ShowPromptAsync(
                "Код восстановления",
                "Введите код восстановления базы данных.",
                "Base64-код");

            if (string.IsNullOrWhiteSpace(key))
            {
                BackupStatusMessage = "Восстановление отменено.";
                return;
            }

            IsBusy = true;
            var restored = await _backupService.RestoreAsync(pickedPath, key);

            if (restored)
            {
                await RefreshBackupsAsync();
                BackupStatusMessage = "База данных восстановлена.";
            }
            else
            {
                BackupStatusMessage = "Не удалось восстановить: неверный код или повреждённая копия.";
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Ошибка восстановления: {ex.Message}";
        }
        finally
        {
            IsBusy = false;

            if (pickedPath is not null && File.Exists(pickedPath))
            {
                File.Delete(pickedPath);
            }
        }
    }

    private async Task ImportLegacyDatabaseAsync()
    {
        string? pickedPath = null;
        try
        {
            if (await _database.CountCorobsAsync() > 0)
            {
                LegacyImportStatusMessage =
                    "Импорт возможен только в пустую базу. Сейчас в базе уже есть короба.";
                return;
            }

            pickedPath = await _fileTransfer.PickAsync("*/*");
            if (pickedPath is null)
            {
                LegacyImportStatusMessage = "Файл не выбран.";
                return;
            }

            var confirmed = await _dialogs.ShowConfirmationAsync(
                "Импорт старой базы",
                "Будут добавлены короба и марки из файла barcode.db. Продолжить?",
                "Импортировать",
                "Отмена");

            if (!confirmed)
            {
                LegacyImportStatusMessage = "Импорт отменён.";
                return;
            }

            IsBusy = true;
            var result = await _database.ImportLegacyDatabaseAsync(pickedPath);

            if (result.Imported)
            {
                LegacyImportStatusMessage =
                    $"Импортировано: коробов {result.Corobs}, марок {result.Barcodes}." +
                    (result.OrphanBarcodes > 0
                        ? $" Пропущено марок без короба: {result.OrphanBarcodes}."
                        : string.Empty);
            }
            else if (!string.IsNullOrWhiteSpace(result.Error))
            {
                LegacyImportStatusMessage = $"Ошибка импорта: {result.Error}";
            }
            else
            {
                LegacyImportStatusMessage =
                    "В выбранном файле не найдено коробов и марок.";
            }
        }
        catch (Exception ex)
        {
            LegacyImportStatusMessage = $"Ошибка импорта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;

            if (pickedPath is not null && File.Exists(pickedPath))
            {
                File.Delete(pickedPath);
            }
        }
    }

    private async Task<string> GetRecoveryCodeAsync()
    {
        var code = await _keyProvider.GetOrCreateKeyAsync();
        RecoveryCode = code;
        return code;
    }

    private async Task RefreshBackupsAsync()
    {
        try
        {
            var backups = await _backupService.GetBackupsAsync();
            if (backups.Count == 0)
            {
                LastBackupText = "Копий нет.";
                return;
            }

            var latest = backups[0];
            LastBackupText =
                $"Последняя: {latest.CreatedAt:dd.MM.yyyy HH:mm} · {FormatKind(latest.Kind)} · {FormatSize(latest.SizeBytes)}";
        }
        catch (Exception ex)
        {
            LastBackupText = $"Ошибка чтения копий: {ex.Message}";
        }
    }

    private static string FormatKind(BackupKind kind)
    {
        return kind switch
        {
            BackupKind.Daily => "ежедневная",
            BackupKind.Weekly => "еженедельная",
            BackupKind.Monthly => "ежемесячная",
            _ => "ручная"
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} Б";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} КБ";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} МБ";
    }

    private Task ShowAboutAsync()
    {
        return _dialogs.ShowAlertAsync(
            "О программе",
            "Сканер QR\nВерсия 1.0\n.NET 10 / MAUI",
            "OK");
    }
}
