using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace QRScanner.App.Services;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckAsync(
        CancellationToken cancellationToken = default);

    Task<AppUpdateInstallResult> DownloadAndInstallAsync(
        AppUpdateInfo update,
        CancellationToken cancellationToken = default);
}

public sealed record AppUpdateInfo(
    string Version,
    int VersionCode,
    string FileName,
    string Notes,
    string Sha256);

public sealed record AppUpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    string Message,
    AppUpdateInfo? Update = null);

public sealed record AppUpdateInstallResult(bool Success, string Message);

public sealed class AppUpdateService : IAppUpdateService
{
    private const string UpdatesFolder = "Обновления";
    private const string ManifestFileName = "version.json";

    private readonly ISmbSettingsService _smbSettings;
    private readonly ISmbFileTransferService _smbTransfer;

    public AppUpdateService(
        ISmbSettingsService smbSettings,
        ISmbFileTransferService smbTransfer)
    {
        _smbSettings = smbSettings;
        _smbTransfer = smbTransfer;
    }

    public async Task<AppUpdateCheckResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _smbSettings.LoadAsync();
        if (!settings.IsConfigured)
        {
            return new AppUpdateCheckResult(
                false,
                false,
                "Сначала настройте общую папку на компьютере в разделе «Компьютер (SMB)».");
        }

        using var memory = new MemoryStream();
        var download = await _smbTransfer.DownloadAsync(
            settings,
            UpdatesFolder,
            ManifestFileName,
            memory,
            cancellationToken);

        if (!download.Success)
        {
            return new AppUpdateCheckResult(false, false, download.Message);
        }

        AppUpdateInfo? update;
        try
        {
            var text = Encoding.UTF8
                .GetString(memory.ToArray())
                .Trim('\uFEFF', ' ', '\t', '\r', '\n');

            update = ParseManifest(text);
        }
        catch (Exception ex)
        {
            return new AppUpdateCheckResult(
                false,
                false,
                $"Файл version.json повреждён: {ex.Message}");
        }

        if (update is null)
        {
            return new AppUpdateCheckResult(
                false,
                false,
                "В файле version.json нужны поля version, versionCode и file.");
        }

        var currentCode = GetCurrentVersionCode();
        var currentVersion = AppInfo.Current.VersionString;

        if (update.VersionCode <= currentCode)
        {
            return new AppUpdateCheckResult(
                true,
                false,
                $"Установлена последняя версия: {currentVersion} ({currentCode}).");
        }

        var notes = string.IsNullOrWhiteSpace(update.Notes)
            ? string.Empty
            : $"\n\n{update.Notes}";

        return new AppUpdateCheckResult(
            true,
            true,
            $"Доступна версия {update.Version} ({update.VersionCode}).{notes}",
            update);
    }

    public async Task<AppUpdateInstallResult> DownloadAndInstallAsync(
        AppUpdateInfo update,
        CancellationToken cancellationToken = default)
    {
        var settings = await _smbSettings.LoadAsync();
        if (!settings.IsConfigured)
        {
            return new AppUpdateInstallResult(
                false,
                "Сначала настройте общую папку на компьютере.");
        }

        var context = Android.App.Application.Context;
        var packageName = context.PackageName ?? string.Empty;
        var packageManager = context.PackageManager;

        if (packageManager is null)
        {
            return new AppUpdateInstallResult(
                false,
                "Не удалось получить доступ к установщику Android.");
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26) &&
            !packageManager.CanRequestPackageInstalls())
        {
            OpenUnknownSourcesSettings(context, packageName);
            return new AppUpdateInstallResult(
                false,
                "Разрешите установку приложений из этого источника и нажмите «Проверить обновление» ещё раз.");
        }

        var directory = Path.Combine(FileSystem.CacheDirectory, "updates");
        Directory.CreateDirectory(directory);

        var apkPath = Path.Combine(directory, update.FileName);

        if (!ValidateApk(apkPath, update, packageName, packageManager, out _))
        {
            TryDelete(apkPath);

            try
            {
                await using var file = File.Create(apkPath);
                var download = await _smbTransfer.DownloadAsync(
                    settings,
                    UpdatesFolder,
                    update.FileName,
                    file,
                    cancellationToken);

                if (!download.Success)
                {
                    TryDelete(apkPath);
                    return new AppUpdateInstallResult(false, download.Message);
                }

                if (download.Size <= 0)
                {
                    TryDelete(apkPath);
                    return new AppUpdateInstallResult(
                        false,
                        "С компьютера получен пустой файл обновления.");
                }
            }
            catch (OperationCanceledException)
            {
                TryDelete(apkPath);
                throw;
            }
            catch (Exception ex)
            {
                TryDelete(apkPath);
                return new AppUpdateInstallResult(
                    false,
                    $"Ошибка скачивания: {ex.Message}");
            }
        }

        if (!ValidateApk(apkPath, update, packageName, packageManager, out var apkError))
        {
            TryDelete(apkPath);
            return new AppUpdateInstallResult(false, apkError);
        }

        if (!VerifyHash(apkPath, update.Sha256, out var hashError))
        {
            TryDelete(apkPath);
            return new AppUpdateInstallResult(false, hashError);
        }

        return StartInstaller(
            apkPath,
            update,
            context,
            packageName,
            packageManager);
    }

    private static bool ValidateApk(
        string apkPath,
        AppUpdateInfo update,
        string packageName,
        Android.Content.PM.PackageManager packageManager,
        out string error)
    {
        error = string.Empty;

        if (!File.Exists(apkPath) || new FileInfo(apkPath).Length <= 0)
        {
            error = "Файл обновления не скачан.";
            return false;
        }

        Android.Content.PM.PackageInfo? archive;
        try
        {
#pragma warning disable CA1422
            archive = packageManager.GetPackageArchiveInfo(
                apkPath,
                (Android.Content.PM.PackageInfoFlags)0);
#pragma warning restore CA1422
        }
        catch (Exception ex)
        {
            error = $"Не удалось прочитать APK: {ex.Message}";
            return false;
        }

        if (archive is null)
        {
            error = "Скачанный файл не является APK.";
            return false;
        }

        if (!string.Equals(archive.PackageName, packageName, StringComparison.Ordinal))
        {
            error = $"APK относится к другому приложению: {archive.PackageName}.";
            return false;
        }

        var currentCode = GetCurrentVersionCode();
        var archiveCode = GetVersionCode(archive);

        if (archiveCode <= currentCode)
        {
            error = $"В файле версия {archiveCode}, установлена {currentCode}. Обновление не требуется.";
            return false;
        }

        if (archiveCode != update.VersionCode)
        {
            error = $"Версия APK {archiveCode} не совпадает с version.json ({update.VersionCode}).";
            return false;
        }

        return true;
    }

    private static bool VerifyHash(
        string apkPath,
        string expectedHash,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(apkPath);
            var hash = Convert.ToHexString(SHA256.HashData(stream));

            if (!string.Equals(
                hash,
                expectedHash.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                error = "APK повреждён: SHA-256 не совпадает. Попробуйте скачать ещё раз.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Не удалось проверить APK: {ex.Message}";
            return false;
        }
    }

    private static AppUpdateInstallResult StartInstaller(
        string apkPath,
        AppUpdateInfo update,
        Android.Content.Context context,
        string packageName,
        Android.Content.PM.PackageManager packageManager)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) &&
            !packageManager.CanRequestPackageInstalls())
        {
            OpenUnknownSourcesSettings(context, packageName);
            return new AppUpdateInstallResult(
                false,
                "Разрешите установку приложений из этого источника и нажмите «Проверить обновление» ещё раз.");
        }

        try
        {
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                context,
                packageName + ".fileprovider",
                new Java.IO.File(apkPath));

            using var intent = new Android.Content.Intent(
                Android.Content.Intent.ActionView);
            intent.SetDataAndType(
                uri,
                "application/vnd.android.package-archive");
            intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);

            context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            return new AppUpdateInstallResult(
                false,
                $"Не удалось открыть установщик: {ex.Message}");
        }

        return new AppUpdateInstallResult(
            true,
            $"Открыт установщик Android для версии {update.Version}. " +
            "Нажмите «Обновить» — база и настройки сохранятся.\n\n" +
            "Если Play Protect предложит проверку, нажмите «Проверить», затем «Установить».");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
    private static void OpenUnknownSourcesSettings(
        Android.Content.Context context,
        string packageName)
    {
        try
        {
            using var intent = new Android.Content.Intent(
                Android.Provider.Settings.ActionManageUnknownAppSources);
            intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch
        {
            try
            {
                using var intent = new Android.Content.Intent(
                    Android.Provider.Settings.ActionSettings);
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
            catch
            {
                // The user can open Android settings manually.
            }
        }
    }

    private static AppUpdateInfo? ParseManifest(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("ожидался объект JSON");
        }

        var version = ReadString(root, "version");
        var versionCode = ReadInt(root, "versionCode");
        var fileName = ReadString(root, "file");
        var notes = ReadString(root, "notes");
        var sha256 = ReadString(root, "sha256");

        if (string.IsNullOrWhiteSpace(version) ||
            versionCode <= 0 ||
            string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var normalized = fileName.Replace('\\', '/');
        var name = normalized[(normalized.LastIndexOf('/') + 1)..].Trim();

        if (!name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "поле file должно указывать на файл .apk");
        }

        return new AppUpdateInfo(
            version,
            versionCode,
            name,
            notes ?? string.Empty,
            (sha256 ?? string.Empty).Trim());
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return 0;
        }

        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static int GetCurrentVersionCode()
    {
        return int.TryParse(
            AppInfo.Current.BuildString,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var code)
            ? code
            : 0;
    }

    private static int GetVersionCode(Android.Content.PM.PackageInfo info)
    {
#pragma warning disable CA1422
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            return (int)info.LongVersionCode;
        }

        return info.VersionCode;
#pragma warning restore CA1422
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A leftover APK in the cache is harmless.
        }
    }
}
