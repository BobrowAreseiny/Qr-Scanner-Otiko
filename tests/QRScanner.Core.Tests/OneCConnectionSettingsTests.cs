using QRScanner.Core.Integration;

namespace QRScanner.Core.Tests;

public sealed class OneCConnectionSettingsTests
{
    [Fact]
    public void OrdersUrl_CombinesBaseAndPath()
    {
        var settings = new OneCConnectionSettings
        {
            BaseUrl = "http://server/1c/",
            OrdersPath = "api/qrscanner/orders"
        };

        Assert.Equal("http://server/1c/api/qrscanner/orders", settings.OrdersUrl);
    }

    [Fact]
    public void HealthUrl_UsesHealthPath()
    {
        var settings = new OneCConnectionSettings
        {
            BaseUrl = "https://1c.local",
            HealthPath = "/health"
        };

        Assert.Equal("https://1c.local/health", settings.HealthUrl);
    }

    [Fact]
    public void Normalize_TrimsValuesAndRestoresDefaults()
    {
        var settings = new OneCConnectionSettings
        {
            BaseUrl = "  http://server/1c  ",
            OrdersPath = "  ",
            HealthPath = "  ",
            Token = "  token  ",
            Login = "  user  ",
            Warehouse = "  ",
            TimeoutSeconds = 1
        };

        var normalized = settings.Normalize();

        Assert.Equal("http://server/1c", normalized.BaseUrl);
        Assert.Equal(OneCConnectionSettings.DefaultOrdersPath, normalized.OrdersPath);
        Assert.Equal(OneCConnectionSettings.DefaultHealthPath, normalized.HealthPath);
        Assert.Equal("token", normalized.Token);
        Assert.Equal("user", normalized.Login);
        Assert.Equal(OneCConnectionSettings.DefaultWarehouse, normalized.Warehouse);
        Assert.Equal(5, normalized.TimeoutSeconds);
    }

    [Fact]
    public void Normalize_ClampsHighTimeout()
    {
        var settings = new OneCConnectionSettings { TimeoutSeconds = 9999 };

        Assert.Equal(300, settings.Normalize().TimeoutSeconds);
    }

    [Fact]
    public void Validate_RequiresBaseUrl()
    {
        var errors = new OneCConnectionSettings().Validate();

        Assert.Contains("Не задан адрес API.", errors);
    }

    [Theory]
    [InlineData("ftp://server")]
    [InlineData("server/1c")]
    [InlineData("")]
    public void Validate_RejectsInvalidBaseUrl(string baseUrl)
    {
        var errors = new OneCConnectionSettings { BaseUrl = baseUrl }.Validate();

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_RequiresBearerToken()
    {
        var settings = new OneCConnectionSettings
        {
            BaseUrl = "http://server",
            AuthType = OneCAuthType.Bearer
        };

        Assert.Contains("Не задан Bearer-токен.", settings.Validate());
    }

    [Fact]
    public void Validate_RequiresBasicCredentials()
    {
        var settings = new OneCConnectionSettings
        {
            BaseUrl = "http://server",
            AuthType = OneCAuthType.Basic,
            Login = "user"
        };

        Assert.Contains("Не заданы логин и пароль.", settings.Validate());
    }

    [Fact]
    public void Validate_AcceptsCompleteSettings()
    {
        var settings = new OneCConnectionSettings
        {
            BaseUrl = "https://1c.local",
            AuthType = OneCAuthType.Basic,
            Login = "user",
            Password = "secret"
        };

        Assert.Empty(settings.Validate());
    }
}
