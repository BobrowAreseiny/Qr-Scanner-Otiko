namespace QRScanner.Core.Backup;

public enum BackupKind
{
    Daily,
    Weekly,
    Monthly,
    Manual
}

public sealed record BackupEntry(BackupKind Kind, DateTime CreatedAt);

public static class BackupPolicy
{
    public const int DailyRetention = 7;
    public const int WeeklyRetention = 4;
    public const int MonthlyRetention = 3;

    public static IReadOnlyList<BackupKind> GetDueKinds(
        DateTime now,
        IEnumerable<BackupEntry> backups)
    {
        var entries = backups.ToList();
        var result = new List<BackupKind>();

        if (ShouldCreateDaily(now, entries))
        {
            result.Add(BackupKind.Daily);
        }

        if (ShouldCreateWeekly(now, entries))
        {
            result.Add(BackupKind.Weekly);
        }

        if (ShouldCreateMonthly(now, entries))
        {
            result.Add(BackupKind.Monthly);
        }

        return result;
    }

    public static bool ShouldCreateDaily(DateTime now, IEnumerable<BackupEntry> backups)
    {
        return !backups.Any(backup =>
            backup.Kind == BackupKind.Daily &&
            backup.CreatedAt.Date == now.Date);
    }

    public static bool ShouldCreateWeekly(DateTime now, IEnumerable<BackupEntry> backups)
    {
        var year = System.Globalization.ISOWeek.GetYear(now);
        var week = System.Globalization.ISOWeek.GetWeekOfYear(now);

        return !backups.Any(backup =>
            backup.Kind == BackupKind.Weekly &&
            System.Globalization.ISOWeek.GetYear(backup.CreatedAt) == year &&
            System.Globalization.ISOWeek.GetWeekOfYear(backup.CreatedAt) == week);
    }

    public static bool ShouldCreateMonthly(DateTime now, IEnumerable<BackupEntry> backups)
    {
        return !backups.Any(backup =>
            backup.Kind == BackupKind.Monthly &&
            backup.CreatedAt.Year == now.Year &&
            backup.CreatedAt.Month == now.Month);
    }

    public static int GetRetention(BackupKind kind)
    {
        return kind switch
        {
            BackupKind.Daily => DailyRetention,
            BackupKind.Weekly => WeeklyRetention,
            BackupKind.Monthly => MonthlyRetention,
            _ => int.MaxValue
        };
    }

    public static IReadOnlyList<BackupEntry> SelectExpired(
        BackupKind kind,
        IEnumerable<BackupEntry> backups)
    {
        return backups
            .Where(backup => backup.Kind == kind)
            .OrderByDescending(backup => backup.CreatedAt)
            .Skip(GetRetention(kind))
            .ToList();
    }
}
