using QRScanner.Core.Logging;

namespace QRScanner.Core.Tests;

public sealed class LogSanitizerTests
{
    [Theory]
    [InlineData("Authorization: Bearer abc123", "Authorization: Bearer ***")]
    [InlineData("authorization: basic dXNlcjpwYXNz", "authorization: basic ***")]
    [InlineData("password=secret", "password=***")]
    [InlineData("token: abcdef", "token: ***")]
    [InlineData("api_key=12345", "api_key=***")]
    [InlineData("ключ: значение", "ключ: ***")]
    public void Sanitize_MasksSecrets(string source, string expected)
    {
        Assert.Equal(expected, LogSanitizer.Sanitize(source));
    }

    [Fact]
    public void Sanitize_MasksJsonPassword()
    {
        var result = LogSanitizer.Sanitize("{\"password\":\"secret\",\"name\":\"test\"}");

        Assert.DoesNotContain("secret", result);
        Assert.Contains("\"name\":\"test\"", result);
    }

    [Fact]
    public void Sanitize_LeavesRegularTextUnchanged()
    {
        const string text = "Заказ сохранён, марок 12, короб №1";

        Assert.Equal(text, LogSanitizer.Sanitize(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_HandlesEmptyValues(string? source)
    {
        Assert.Equal(string.Empty, LogSanitizer.Sanitize(source));
    }
}
