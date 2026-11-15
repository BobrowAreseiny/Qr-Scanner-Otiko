using QRScanner.Core.Logging;

namespace QRScanner.Core.Tests;

public sealed class LogFileNamesTests
{
    [Fact]
    public void ForDate_BuildsDailyName()
    {
        var name = LogFileNames.ForDate(new DateTime(2026, 11, 15));

        Assert.Equal("qrscanner-2026-11-15.log", name);
    }

    [Fact]
    public void TryParseDate_ReadsDailyName()
    {
        var ok = LogFileNames.TryParseDate("qrscanner-2026-11-15.log", out var date);

        Assert.True(ok);
        Assert.Equal(new DateTime(2026, 11, 15), date);
    }

    [Theory]
    [InlineData("app-2026-11-15.log")]
    [InlineData("qrscanner-2026-11-15.txt")]
    [InlineData("qrscanner-2026-13-15.log")]
    [InlineData("qrscanner.log")]
    public void TryParseDate_RejectsForeignNames(string name)
    {
        Assert.False(LogFileNames.TryParseDate(name, out _));
    }
}
