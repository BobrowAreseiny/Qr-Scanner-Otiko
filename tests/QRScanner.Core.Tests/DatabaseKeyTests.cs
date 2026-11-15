using QRScanner.Core.Security;

namespace QRScanner.Core.Tests;

public sealed class DatabaseKeyTests
{
    [Fact]
    public void IsValid_AcceptsThirtyTwoBytesInBase64()
    {
        var key = Convert.ToBase64String(new byte[32]);

        Assert.True(DatabaseKey.IsValid(key));
    }

    [Fact]
    public void IsValid_TrimsWhitespace()
    {
        var key = Convert.ToBase64String(new byte[32]);

        Assert.True(DatabaseKey.IsValid($"  {key}\n"));
    }

    [Fact]
    public void IsValid_RejectsDifferentKeyLength()
    {
        var key = Convert.ToBase64String(new byte[16]);

        Assert.False(DatabaseKey.IsValid(key));
    }

    [Fact]
    public void IsValid_RejectsInvalidBase64()
    {
        Assert.False(DatabaseKey.IsValid("not-a-base64-key"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_RejectsEmptyValues(string? key)
    {
        Assert.False(DatabaseKey.IsValid(key));
    }

    [Fact]
    public void Normalize_TrimsValue()
    {
        Assert.Equal("abc", DatabaseKey.Normalize("  abc \n"));
    }
}
