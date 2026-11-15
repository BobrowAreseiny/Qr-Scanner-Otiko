namespace QRScanner.Core.Integration;

public enum OneCAuthType
{
    None,
    Bearer,
    Basic
}

public sealed record OneCConnectionSettings
{
    public const string DefaultOrdersPath = "/api/qrscanner/orders";
    public const string DefaultHealthPath = "/api/qrscanner/health";
    public const string DefaultWarehouse = "Основной склад";
    public const int DefaultTimeoutSeconds = 30;

    public string BaseUrl { get; init; } = string.Empty;

    public string OrdersPath { get; init; } = DefaultOrdersPath;

    public string HealthPath { get; init; } = DefaultHealthPath;

    public OneCAuthType AuthType { get; init; } = OneCAuthType.None;

    public string Token { get; init; } = string.Empty;

    public string Login { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Warehouse { get; init; } = DefaultWarehouse;

    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    public bool AllowInvalidCertificate { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    public string OrdersUrl => BuildUrl(BaseUrl, OrdersPath);

    public string HealthUrl => BuildUrl(BaseUrl, HealthPath);

    public static OneCConnectionSettings Default => new();

    public OneCConnectionSettings Normalize()
    {
        return this with
        {
            BaseUrl = BaseUrl.Trim(),
            OrdersPath = NormalizePath(OrdersPath, DefaultOrdersPath),
            HealthPath = NormalizePath(HealthPath, DefaultHealthPath),
            Token = Token.Trim(),
            Login = Login.Trim(),
            Warehouse = string.IsNullOrWhiteSpace(Warehouse)
                ? DefaultWarehouse
                : Warehouse.Trim(),
            TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 300)
        };
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!IsConfigured)
        {
            errors.Add("Не задан адрес API.");
            return errors;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Адрес API должен начинаться с http:// или https://.");
        }

        if (AuthType == OneCAuthType.Bearer && string.IsNullOrWhiteSpace(Token))
        {
            errors.Add("Не задан Bearer-токен.");
        }

        if (AuthType == OneCAuthType.Basic &&
            (string.IsNullOrWhiteSpace(Login) || string.IsNullOrEmpty(Password)))
        {
            errors.Add("Не заданы логин и пароль.");
        }

        return errors;
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        var basePart = baseUrl.Trim().TrimEnd('/');
        var pathPart = path.Trim();

        if (pathPart.Length == 0)
        {
            return basePart;
        }

        if (!pathPart.StartsWith('/'))
        {
            pathPart = "/" + pathPart;
        }

        return basePart + pathPart;
    }

    private static string NormalizePath(string path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallback;
        }

        var trimmed = path.Trim();
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}
