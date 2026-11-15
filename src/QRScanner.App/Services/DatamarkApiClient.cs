using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QRScanner.App.Services.Datamark;
using QRScanner.Core;
using QRScanner.Core.Barcodes;

namespace QRScanner.App.Services;

public sealed class DatamarkApiClient : IDatamarkApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan TokenCacheLifetime = TimeSpan.FromMinutes(15);

    private readonly HttpClient _httpClient;
    private readonly ILogger<DatamarkApiClient> _logger;

    private string? _token;
    private string? _tokenBaseUrl;
    private string? _tokenLogin;
    private string? _tokenPassword;
    private DateTime _tokenObtainedUtc;

    public DatamarkApiClient(ILogger<DatamarkApiClient> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<bool> TestConnectionAsync(
        ApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await AuthenticateAsync(settings, cancellationToken, force: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось подключиться к Электронному знаку");
            return false;
        }
    }

    public async Task<ProductInfo?> FindProductAsync(
        string gtin,
        ApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalizedGtin = GtinValidator.Normalize(gtin);
        if (normalizedGtin is null || !GtinValidator.IsValid(normalizedGtin))
        {
            return null;
        }

        var token = await GetTokenAsync(settings, cancellationToken);
        var item = await FindItemAsync(normalizedGtin, settings, token, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var size = FindSize(item.@params);

        return new ProductInfo(
            string.IsNullOrWhiteSpace(item.gtin) ? normalizedGtin : item.gtin!.Trim(),
            Clean(item.articul),
            Clean(size),
            Clean(item.name),
            item.gtin_check,
            Clean(item.group),
            item.is_my ?? false);
    }

    private async Task<ItemDto?> FindItemAsync(
        string gtin,
        ApiSettings settings,
        string token,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(settings.BaseUrl, "items/findItems");

        using var response = await SendFindItemsAsync(uri, gtin, token, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var newToken = await AuthenticateAsync(settings, cancellationToken, force: true);
            using var retryResponse = await SendFindItemsAsync(uri, gtin, newToken, cancellationToken);
            return await ReadFindItemsResponseAsync(retryResponse, cancellationToken);
        }

        return await ReadFindItemsResponseAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendFindItemsAsync(
        Uri uri,
        string gtin,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new { search = gtin })
        };
        request.Headers.TryAddWithoutValidation("Token", token);

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<ItemDto?> ReadFindItemsResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiErrorAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<FindItemsResponse>(
            JsonOptions,
            cancellationToken);

        return result?.items?.FirstOrDefault();
    }

    private async Task<string> GetTokenAsync(
        ApiSettings settings,
        CancellationToken cancellationToken)
    {
        if (IsCachedTokenValid(settings))
        {
            return _token!;
        }

        return await AuthenticateAsync(settings, cancellationToken);
    }

    private async Task<string> AuthenticateAsync(
        ApiSettings settings,
        CancellationToken cancellationToken,
        bool force = false)
    {
        if (!force && IsCachedTokenValid(settings))
        {
            return _token!;
        }

        if (!settings.HasCredentials)
        {
            throw new InvalidOperationException("Не заданы логин и пароль Электронного знака.");
        }

        var uri = BuildUri(settings.BaseUrl, "auth");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(settings.Login.Trim()), "username" },
            { new StringContent(settings.Password), "password" }
        };

        using var response = await _httpClient.PostAsync(uri, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiErrorAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(
            JsonOptions,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(result?.token))
        {
            throw new InvalidOperationException("Электронный знак не вернул токен авторизации.");
        }

        _token = result.token;
        _tokenBaseUrl = settings.BaseUrl;
        _tokenLogin = settings.Login;
        _tokenPassword = settings.Password;
        _tokenObtainedUtc = DateTime.UtcNow;
        return _token;
    }

    private bool IsCachedTokenValid(ApiSettings settings)
    {
        return !string.IsNullOrWhiteSpace(_token) &&
               DateTime.UtcNow - _tokenObtainedUtc < TokenCacheLifetime &&
               string.Equals(_tokenBaseUrl, settings.BaseUrl, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(_tokenLogin, settings.Login, StringComparison.Ordinal) &&
               string.Equals(_tokenPassword, settings.Password, StringComparison.Ordinal);
    }

    private static async Task ThrowApiErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var message = $"HTTP {statusCode}";
        var details = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("message", out var messageProperty))
            {
                message = messageProperty.GetString() ?? message;
            }

            if (root.TryGetProperty("details", out var detailsProperty) &&
                detailsProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in detailsProperty.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        details.Add(value);
                    }
                }
            }
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                message = body.Length > 300 ? body[..300] : body;
            }
        }

        if (details.Count > 0)
        {
            message = $"{message}: {string.Join("; ", details)}";
        }

        throw new DatamarkApiException(statusCode, message, details);
    }

    private static string? FindSize(List<ParamDto>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        var exact = parameters.FirstOrDefault(parameter => string.Equals(
            parameter.name,
            "Размер в штихмассовой системе",
            StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact.display_value ?? exact.value;
        }

        return parameters
            .FirstOrDefault(parameter =>
                parameter.name?.Contains("Размер", StringComparison.OrdinalIgnoreCase) == true)
            ?.display_value;
    }

    private static Uri BuildUri(string baseUrl, string relative)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? AppConstants.ApiBaseUrl
            : baseUrl.Trim();

        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        return new Uri(new Uri(normalized, UriKind.Absolute), relative);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
