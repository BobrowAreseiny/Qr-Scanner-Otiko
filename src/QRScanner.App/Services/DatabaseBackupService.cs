using System.Globalization;
using Microsoft.Extensions.Logging;
using QRScanner.App.Data;
using QRScanner.Core.Backup;

namespace QRScanner.App.Services;

public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private const string FilePrefix = "qrscanner-";
    private const string FileExtension = ".db";
    private const string FileNameFormat = "yyyyMMdd-HHmmss-fff";

    private readonly IDatabaseService _database;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(
        IDatabaseService database,
        ILogger<DatabaseBackupService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public string BackupDirectory => Path.Combine(FileSystem.AppDataDirectory, "Backups");

    public async Task<int> EnsureAutomaticBackupsAsync()
    {
        var backups = await GetBackupsAsync();
        var entries = backups.Select(backup => new BackupEntry(backup.Kind, backup.CreatedAt));
        var dueKinds = BackupPolicy.GetDueKinds(DateTime.Now, entries);

        foreach (var kind in dueKinds)
        {
            await CreateBackupAsync(kind);
        }

        return dueKinds.Count;
    }

    public async Task<BackupInfo> CreateBackupAsync(BackupKind kind)
    {
        Directory.CreateDirectory(BackupDirectory);

        var createdAt = DateTime.Now;
        var path = Path.Combine(BackupDirectory, BuildFileName(kind, createdAt));

        await _database.CreateSnapshotAsync(path);

        var info = new BackupInfo(
            path,
            kind,
            createdAt,
            new FileInfo(path).Length);

        _logger.LogInformation(
            "Создана копия БД: {Kind}, {Path}, {SizeBytes} байт",
            kind,
            path,
            info.SizeBytes);

        if (kind != BackupKind.Manual)
        {
            await TrimAsync();
        }

        return info;
    }

    public Task<IReadOnlyList<BackupInfo>> GetBackupsAsync()
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return Task.FromResult<IReadOnlyList<BackupInfo>>([]);
        }

        var backups = new List<BackupInfo>();

        foreach (var path in Directory.EnumerateFiles(BackupDirectory, $"*{FileExtension}"))
        {
            if (TryParseBackup(path, out var info))
            {
                backups.Add(info);
            }
        }

        backups.Sort((left, right) => right.CreatedAt.CompareTo(left.CreatedAt));
        return Task.FromResult<IReadOnlyList<BackupInfo>>(backups);
    }

    public async Task<BackupInfo?> GetLatestBackupAsync()
    {
        var backups = await GetBackupsAsync();
        return backups.FirstOrDefault();
    }

    public Task<bool> RestoreAsync(string backupPath, string recoveryKey)
    {
        return _database.RestoreAsync(backupPath, recoveryKey);
    }

    private async Task TrimAsync()
    {
        var backups = await GetBackupsAsync();
        var entries = backups.Select(backup => new BackupEntry(backup.Kind, backup.CreatedAt));

        foreach (var kind in new[]
                 {
                     BackupKind.Daily,
                     BackupKind.Weekly,
                     BackupKind.Monthly
                 })
        {
            var expired = BackupPolicy.SelectExpired(kind, entries);

            foreach (var expiredEntry in expired)
            {
                var backup = backups.FirstOrDefault(item =>
                    item.Kind == expiredEntry.Kind &&
                    item.CreatedAt == expiredEntry.CreatedAt);

                if (backup is null || !File.Exists(backup.Path))
                {
                    continue;
                }

                File.Delete(backup.Path);
                _logger.LogInformation("Удалена устаревшая копия БД: {Path}", backup.Path);
            }
        }
    }

    private static string BuildFileName(BackupKind kind, DateTime createdAt)
    {
        var suffix = kind.ToString().ToLowerInvariant();
        return $"{FilePrefix}{suffix}-{createdAt.ToString(FileNameFormat, CultureInfo.InvariantCulture)}{FileExtension}";
    }

    private static bool TryParseBackup(string path, out BackupInfo info)
    {
        info = null!;

        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = name[FilePrefix.Length..].Split('-');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!Enum.TryParse<BackupKind>(parts[0], ignoreCase: true, out var kind))
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                $"{parts[1]}-{parts[2]}-{parts[3]}",
                FileNameFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var createdAt))
        {
            return false;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        info = new BackupInfo(
            path,
            kind,
            createdAt,
            new FileInfo(path).Length);

        return true;
    }
}
