using QRScanner.Core.Backup;

namespace QRScanner.Core.Tests;

public sealed class BackupPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 15, 30, 0);

    [Fact]
    public void GetDueKinds_WhenNoBackups_ReturnsAllAutomaticKinds()
    {
        var due = BackupPolicy.GetDueKinds(Now, []);

        Assert.Equal(
            [BackupKind.Daily, BackupKind.Weekly, BackupKind.Monthly],
            due);
    }

    [Fact]
    public void GetDueKinds_WhenCurrentPeriodsExist_ReturnsEmpty()
    {
        var backups = new[]
        {
            new BackupEntry(BackupKind.Daily, new DateTime(2026, 11, 14, 8, 0, 0)),
            new BackupEntry(BackupKind.Weekly, new DateTime(2026, 11, 9, 8, 0, 0)),
            new BackupEntry(BackupKind.Monthly, new DateTime(2026, 11, 1, 8, 0, 0))
        };

        var due = BackupPolicy.GetDueKinds(Now, backups);

        Assert.Empty(due);
    }

    [Fact]
    public void GetDueKinds_ReturnsOnlyMissingPeriods()
    {
        var backups = new[]
        {
            new BackupEntry(BackupKind.Daily, new DateTime(2026, 11, 14, 8, 0, 0)),
            new BackupEntry(BackupKind.Weekly, new DateTime(2026, 11, 9, 8, 0, 0))
        };

        var due = BackupPolicy.GetDueKinds(Now, backups);

        Assert.Equal([BackupKind.Monthly], due);
    }

    [Fact]
    public void ShouldCreateWeekly_HandlesIsoYearBoundary()
    {
        var now = new DateTime(2027, 1, 1, 10, 0, 0);
        var sameIsoWeek = new[] { new BackupEntry(BackupKind.Weekly, new DateTime(2026, 12, 31, 8, 0, 0)) };

        Assert.False(BackupPolicy.ShouldCreateWeekly(now, sameIsoWeek));
    }

    [Fact]
    public void GetRetention_ReturnsConfiguredLimits()
    {
        Assert.Equal(7, BackupPolicy.GetRetention(BackupKind.Daily));
        Assert.Equal(4, BackupPolicy.GetRetention(BackupKind.Weekly));
        Assert.Equal(3, BackupPolicy.GetRetention(BackupKind.Monthly));
        Assert.Equal(int.MaxValue, BackupPolicy.GetRetention(BackupKind.Manual));
    }

    [Fact]
    public void ShouldCreateDaily_ChecksCalendarDate()
    {
        var today = new[] { new BackupEntry(BackupKind.Daily, new DateTime(2026, 11, 14, 0, 0, 0)) };
        var yesterday = new[] { new BackupEntry(BackupKind.Daily, new DateTime(2026, 11, 13, 23, 59, 0)) };

        Assert.False(BackupPolicy.ShouldCreateDaily(Now, today));
        Assert.True(BackupPolicy.ShouldCreateDaily(Now, yesterday));
    }

    [Fact]
    public void ShouldCreateWeekly_UsesIsoWeek()
    {
        var sameWeek = new[] { new BackupEntry(BackupKind.Weekly, new DateTime(2026, 11, 9, 8, 0, 0)) };
        var previousWeek = new[] { new BackupEntry(BackupKind.Weekly, new DateTime(2026, 11, 2, 8, 0, 0)) };

        Assert.False(BackupPolicy.ShouldCreateWeekly(Now, sameWeek));
        Assert.True(BackupPolicy.ShouldCreateWeekly(Now, previousWeek));
    }

    [Fact]
    public void ShouldCreateMonthly_UsesCalendarMonth()
    {
        var sameMonth = new[] { new BackupEntry(BackupKind.Monthly, new DateTime(2026, 11, 1, 0, 0, 0)) };
        var previousMonth = new[] { new BackupEntry(BackupKind.Monthly, new DateTime(2026, 10, 31, 23, 59, 0)) };

        Assert.False(BackupPolicy.ShouldCreateMonthly(Now, sameMonth));
        Assert.True(BackupPolicy.ShouldCreateMonthly(Now, previousMonth));
    }

    [Fact]
    public void SelectExpired_Daily_KeepsNewestSeven()
    {
        var backups = Enumerable.Range(0, 8)
            .Select(day => new BackupEntry(BackupKind.Daily, Now.AddDays(-day)))
            .ToList();

        var expired = BackupPolicy.SelectExpired(BackupKind.Daily, backups);

        var item = Assert.Single(expired);
        Assert.Equal(Now.AddDays(-7), item.CreatedAt);
    }

    [Fact]
    public void SelectExpired_Weekly_KeepsNewestFour()
    {
        var backups = Enumerable.Range(0, 5)
            .Select(week => new BackupEntry(BackupKind.Weekly, Now.AddDays(-7 * week)))
            .ToList();

        var expired = BackupPolicy.SelectExpired(BackupKind.Weekly, backups);

        var item = Assert.Single(expired);
        Assert.Equal(Now.AddDays(-28), item.CreatedAt);
    }

    [Fact]
    public void SelectExpired_Monthly_KeepsNewestThree()
    {
        var backups = Enumerable.Range(0, 4)
            .Select(month => new BackupEntry(BackupKind.Monthly, Now.AddMonths(-month)))
            .ToList();

        var expired = BackupPolicy.SelectExpired(BackupKind.Monthly, backups);

        var item = Assert.Single(expired);
        Assert.Equal(Now.AddMonths(-3), item.CreatedAt);
    }

    [Fact]
    public void SelectExpired_Manual_DoesNotExpire()
    {
        var backups = Enumerable.Range(0, 20)
            .Select(index => new BackupEntry(BackupKind.Manual, Now.AddMinutes(-index)))
            .ToList();

        var expired = BackupPolicy.SelectExpired(BackupKind.Manual, backups);

        Assert.Empty(expired);
    }

    [Fact]
    public void SelectExpired_IgnoresOtherKinds()
    {
        var backups = new[]
        {
            new BackupEntry(BackupKind.Daily, Now.AddDays(-10)),
            new BackupEntry(BackupKind.Weekly, Now.AddDays(-1)),
            new BackupEntry(BackupKind.Monthly, Now.AddDays(-2))
        };

        var expired = BackupPolicy.SelectExpired(BackupKind.Weekly, backups);

        Assert.Empty(expired);
    }
}
