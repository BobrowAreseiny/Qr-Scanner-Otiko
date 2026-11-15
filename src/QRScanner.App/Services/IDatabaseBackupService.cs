using QRScanner.Core.Backup;

namespace QRScanner.App.Services;

public sealed record BackupInfo(
    string Path,
    BackupKind Kind,
    DateTime CreatedAt,
    long SizeBytes);

public interface IDatabaseBackupService
{
    Task<int> EnsureAutomaticBackupsAsync();

    Task<BackupInfo> CreateBackupAsync(BackupKind kind);

    Task<IReadOnlyList<BackupInfo>> GetBackupsAsync();

    Task<BackupInfo?> GetLatestBackupAsync();

    Task<bool> RestoreAsync(string backupPath, string recoveryKey);
}
