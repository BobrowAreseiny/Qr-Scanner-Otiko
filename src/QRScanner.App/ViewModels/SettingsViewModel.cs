using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
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
    private readonly IOrderFileExporter _orderFileExporter;
    private readonly ISmbSettingsService _smbSettings;
    private readonly ISmbFileTransferService _smbTransfer;
    private readonly IAppUpdateService _appUpdate;

    public SettingsViewModel(
        INavigationService navigation,
        IDialogService dialogs,
        IApiSettingsService apiSettings,
        IDatamarkApiClient apiClient,
        IDbKeyProvider keyProvider,
        IDatabaseBackupService backupService,
        IFileTransferService fileTransfer,
        IDatabaseService database,
        IOrderFileExporter orderFileExporter,
        ISmbSettingsService smbSettings,
        ISmbFileTransferService smbTransfer,
        IAppUpdateService appUpdate)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _apiSettings = apiSettings;
        _apiClient = apiClient;
        _keyProvider = keyProvider;
        _backupService = backupService;
        _fileTransfer = fileTransfer;
        _database = database;
        _orderFileExporter = orderFileExporter;
        _smbSettings = smbSettings;
        _smbTransfer = smbTransfer;
        _appUpdate = appUpdate;

        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
        OpenSectionCommand = new AsyncRelayCommand<string>(OpenSectionAsync);
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
        CleanupFilesCommand = new AsyncRelayCommand(CleanupFilesAsync);
        SaveSmbCommand = new AsyncRelayCommand(SaveSmbAsync);
        TestSmbCommand = new AsyncRelayCommand(TestSmbAsync);
        ImportSmbCommand = new AsyncRelayCommand(ImportSmbAsync);
        CheckUpdateCommand = new AsyncRelayCommand(CheckUpdateAsync);
    }

    [ObservableProperty]
    private string server = string.Empty;

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

    [ObservableProperty]
    private string fileCleanupStatusMessage = "Очистка удаляет .txt и .png старше 30 дней из Download/Otiko.";

    [ObservableProperty]
    private string smbServer = string.Empty;

    [ObservableProperty]
    private string smbShare = "QRScanner";

    [ObservableProperty]
    private string smbLogin = string.Empty;

    [ObservableProperty]
    private string smbPassword = string.Empty;

    [ObservableProperty]
    private bool isSmbPasswordVisible;

    [ObservableProperty]
    private string smbStatusMessage = "Укажите общую папку на компьютере.";

    [ObservableProperty]
    private string currentVersionText = string.Empty;

    [ObservableProperty]
    private string updateStatusMessage = "Обновление проверяется по общей папке компьютера.";

    [ObservableProperty]
    private bool isUpdateBusy;

    [ObservableProperty]
    private string sectionTitle = "Настройки";

    [ObservableProperty]
    private bool showDatamark;

    [ObservableProperty]
    private bool showSmb;

    [ObservableProperty]
    private bool showUpdate;

    [ObservableProperty]
    private bool showSecurity;

    [ObservableProperty]
    private bool showBackups;

    [ObservableProperty]
    private bool showFiles;

    [ObservableProperty]
    private bool showLegacy;

    [ObservableProperty]
    private bool showAbout;

    [ObservableProperty]
    private string datamarkSummaryText = "Не настроен";

    [ObservableProperty]
    private string smbSummaryText = "Не настроен";

    [ObservableProperty]
    private string backupSummaryText = "Копий нет";

    public bool IsSmbPasswordHidden => !IsSmbPasswordVisible;

    partial void OnIsSmbPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSmbPasswordHidden));
    }

    public IAsyncRelayCommand BackCommand { get; }

    public IAsyncRelayCommand<string> OpenSectionCommand { get; }

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

    public IAsyncRelayCommand CleanupFilesCommand { get; }

    public IAsyncRelayCommand SaveSmbCommand { get; }

    public IAsyncRelayCommand TestSmbCommand { get; }

    public IAsyncRelayCommand ImportSmbCommand { get; }

    public IAsyncRelayCommand CheckUpdateCommand { get; }

    public async Task LoadAsync()
    {
        var settings = await _apiSettings.LoadAsync();
        Server = settings.BaseUrl;
        Login = settings.Login;
        Password = settings.Password;
        IsPasswordVisible = false;
        StatusMessage = settings.HasCredentials
            ? "Данные Электронного знака (Datamark) загружены."
            : "Укажите логин и пароль Электронного знака.";
        DatamarkSummaryText = settings.HasCredentials
            ? "Логин сохранён"
            : "Не настроен";

        var smb = await _smbSettings.LoadAsync();
        SmbServer = smb.Server;
        SmbShare = string.IsNullOrWhiteSpace(smb.Share) ? "QRScanner" : smb.Share;
        SmbLogin = string.IsNullOrWhiteSpace(smb.Domain)
            ? smb.Login
            : $"{smb.Domain}\\{smb.Login}";
        SmbPassword = smb.Password;
        IsSmbPasswordVisible = false;
        SmbStatusMessage = smb.IsConfigured
            ? "Настройки общей папки загружены."
            : "Укажите общую папку на компьютере.";
        SmbSummaryText = smb.IsConfigured
            ? $"{smb.Server} · {smb.Share}"
            : "Не настроен";

        var build = AppInfo.Current.BuildString;
        CurrentVersionText = string.IsNullOrWhiteSpace(build)
            ? AppInfo.Current.VersionString
            : $"{AppInfo.Current.VersionString} ({build})";

        await RefreshBackupsAsync();
    }

    public void ApplySection(string? section)
    {
        var key = string.IsNullOrWhiteSpace(section)
            ? "datamark"
            : section.Trim().ToLowerInvariant();

        if (key is not ("datamark" or "smb" or "update" or "security" or
            "backups" or "files" or "legacy" or "about"))
        {
            key = "datamark";
        }

        ShowDatamark = key == "datamark";
        ShowSmb = key == "smb";
        ShowUpdate = key == "update";
        ShowSecurity = key == "security";
        ShowBackups = key == "backups";
        ShowFiles = key == "files";
        ShowLegacy = key == "legacy";
        ShowAbout = key == "about";

        SectionTitle = key switch
        {
            "smb" => "Компьютер",
            "update" => "Обновление",
            "security" => "Безопасность",
            "backups" => "Копии",
            "files" => "Файлы",
            "legacy" => "Перенос базы",
            "about" => "О программе",
            _ => "Datamark"
        };
    }

    private async Task OpenSectionAsync(string? section)
    {
        var key = string.IsNullOrWhiteSpace(section) ? "datamark" : section;
        await _navigation.GoToAsync(
            $"{nameof(SettingsSectionPage)}?section={Uri.EscapeDataString(key)}");
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

    private static async Task<string?> ReadAccessFileFromExternalFilesAsync()
    {
        var external = Android.App.Application.Context.GetExternalFilesDir(null);
        if (external is null)
        {
            return null;
        }

        var path = Path.Combine(external.AbsolutePath, "QRScanner-access.txt");
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path);
    }

    private static async Task<string?> ReadAccessFileFromDownloadsAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return null;
        }

        var context = Android.App.Application.Context;
        var resolver = context.ContentResolver;
        if (resolver is null)
        {
            return null;
        }

        var collection = Android.Provider.MediaStore.Downloads.ExternalContentUri;
        var projection = new[]
        {
            "_id",
            Android.Provider.MediaStore.IMediaColumns.DisplayName
        };
        var selection =
            $"{Android.Provider.MediaStore.IMediaColumns.DisplayName} = ?";

        using var cursor = resolver.Query(
            collection,
            projection,
            selection,
            new[] { "QRScanner-access.txt" },
            null);

        if (cursor is null || !cursor.MoveToFirst())
        {
            return null;
        }

        var idColumn = cursor.GetColumnIndexOrThrow("_id");
        var id = cursor.GetLong(idColumn);
        var uri = Android.Content.ContentUris.WithAppendedId(collection, id);

        using var stream = resolver.OpenInputStream(uri);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private async Task ImportSmbAsync()
    {
        string? pickedPath = null;

        try
        {
            var accessText = await ReadAccessFileFromExternalFilesAsync();
            if (accessText is null)
            {
                accessText = await ReadAccessFileFromDownloadsAsync();
            }

            if (accessText is null)
            {
                pickedPath = await _fileTransfer.PickAsync("*/*");
                if (pickedPath is null)
                {
                    SmbStatusMessage = "Файл не выбран.";
                    return;
                }

                accessText = await File.ReadAllTextAsync(pickedPath);
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in accessText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 ||
                    trimmed.StartsWith("#", StringComparison.Ordinal) ||
                    trimmed.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = trimmed[..separator].Trim();
                var value = trimmed[(separator + 1)..].Trim();
                values[key] = value;
            }

            if (!values.TryGetValue("Server", out var server) ||
                !values.TryGetValue("Share", out var share) ||
                !values.TryGetValue("User", out var user) ||
                !values.TryGetValue("Password", out var password))
            {
                SmbStatusMessage =
                    "В файле нет строк Server, Share, User и Password.";
                return;
            }

            SmbServer = server;
            SmbShare = share;
            SmbLogin = user;
            SmbPassword = password;
            IsSmbPasswordVisible = false;

            await _smbSettings.SaveAsync(BuildSmbSettings());
            SmbStatusMessage = "Настройки SMB импортированы. Нажмите «Проверить».";
        }
        catch (Exception ex)
        {
            SmbStatusMessage = $"Ошибка импорта: {ex.Message}";
        }
        finally
        {
            if (pickedPath is not null && File.Exists(pickedPath))
            {
                File.Delete(pickedPath);
            }
        }
    }

    private SmbConnectionSettings BuildSmbSettings()
    {
        return new SmbConnectionSettings(
            SmbServer,
            SmbShare,
            string.Empty,
            SmbLogin,
            SmbPassword).Normalize();
    }

    private async Task SaveSmbAsync()
    {
        IsBusy = true;
        try
        {
            var settings = BuildSmbSettings();
            await _smbSettings.SaveAsync(settings);
            SmbStatusMessage = "Настройки общей папки сохранены.";
        }
        catch (Exception ex)
        {
            SmbStatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestSmbAsync()
    {
        IsBusy = true;
        SmbStatusMessage = "Проверка общей папки...";

        try
        {
            var settings = BuildSmbSettings();
            await _smbSettings.SaveAsync(settings);

            var result = await _smbTransfer.TestConnectionAsync(settings);
            SmbStatusMessage = result.Message;
            await _dialogs.ShowAlertAsync("Компьютер", result.Message);
        }
        catch (Exception ex)
        {
            SmbStatusMessage = $"Ошибка проверки: {ex.Message}";
            await _dialogs.ShowAlertAsync("Компьютер", SmbStatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckUpdateAsync()
    {
        if (IsUpdateBusy)
        {
            return;
        }

        IsUpdateBusy = true;
        UpdateStatusMessage = "Проверка обновления...";

        try
        {
            var result = await _appUpdate.CheckAsync();
            UpdateStatusMessage = result.Message;

            if (!result.Success)
            {
                await _dialogs.ShowAlertAsync("Обновление", result.Message, "OK");
                return;
            }

            if (!result.UpdateAvailable || result.Update is null)
            {
                await _dialogs.ShowAlertAsync("Обновление", result.Message, "OK");
                return;
            }

            var confirmed = await _dialogs.ShowConfirmationAsync(
                "Обновление",
                $"{result.Message}\n\nУстановить сейчас? База, короба и настройки сохранятся.\n\n" +
                "Если Play Protect предложит проверку, нажмите «Проверить», затем «Установить».",
                "Установить",
                "Позже");

            if (!confirmed)
            {
                UpdateStatusMessage = "Обновление отложено.";
                return;
            }

            UpdateStatusMessage = "Скачивание обновления с компьютера...";
            var install = await _appUpdate.DownloadAndInstallAsync(result.Update);
            UpdateStatusMessage = install.Message;

            if (!install.Success)
            {
                await _dialogs.ShowAlertAsync("Обновление", install.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Ошибка обновления: {ex.Message}";
            await _dialogs.ShowAlertAsync("Обновление", UpdateStatusMessage, "OK");
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    private async Task CleanupFilesAsync()
    {
        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Очистка старых файлов",
            "Удалить из Download/Otiko файлы .txt и .png старше 30 дней? База, короба и марки не затрагиваются.",
            "Очистить",
            "Отмена");

        if (!confirmed)
        {
            FileCleanupStatusMessage = "Очистка отменена.";
            return;
        }

        try
        {
            var result = await _orderFileExporter.CleanupOldFilesAsync(
                TimeSpan.FromDays(30));

            FileCleanupStatusMessage = result.DeletedCount == 0
                ? "Старых файлов нет."
                : $"Удалено файлов: {result.DeletedCount}. Освобождено: {FormatSize(result.FreedBytes)}.";
        }
        catch (Exception ex)
        {
            FileCleanupStatusMessage = $"Ошибка очистки: {ex.Message}";
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
                BackupSummaryText = "Копий нет";
                return;
            }

            var latest = backups[0];
            LastBackupText =
                $"Последняя: {latest.CreatedAt:dd.MM.yyyy HH:mm} · {FormatKind(latest.Kind)} · {FormatSize(latest.SizeBytes)}";
            BackupSummaryText = $"Последняя: {latest.CreatedAt:dd.MM.yyyy}";
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
            $"Сканер QR\nВерсия {AppInfo.Current.VersionString}\n.NET 10 / MAUI",
            "OK");
    }
}
