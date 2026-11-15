using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QRScanner.App.Services.Datamark;
using QRScanner.Core;

namespace QRScanner.App.Services;

public sealed class DatamarkApiClient : IDatamarkApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<DatamarkApiClient> _logger;

    private string? _token;
    private string? _tokenBaseUrl;
    private string? _tokenLogin;
    private string? _tokenPassword;

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
            var token = await AuthenticateAsync(settings, cancellationToken);
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось подключиться к Datamark");
            return false;
        }
    }

    public async Task<ProductInfo?> FindProductAsync(
        string gtin,
        ApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gtin))
        {
            return null;
        }

        var token = await GetTokenAsync(settings, cancellationToken);
        var item = await FindItemAsync(gtin, settings, token, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var size = item.@params?
            .FirstOrDefault(p => string.Equals(
                p.name,
                "Размер в штихмассовой системе",
                StringComparison.OrdinalIgnoreCase))?
            .display_value;

        return new ProductInfo(
            string.IsNullOrWhiteSpace(item.gtin) ? gtin : item.gtin!.Trim(),
            Clean(item.articul),
            Clean(size));
    }

    private async Task<ItemDto?> FindItemAsync(
        string gtin,
        ApiSettings settings,
        string token,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(settings.BaseUrl, "items/findItems");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new { search = gtin })
        };
        request.Headers.TryAddWithoutValidation("Token", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var newToken = await AuthenticateAsync(settings, cancellationToken, force: true);
            using var retry = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(new { search = gtin })
            };
            retry.Headers.TryAddWithoutValidation("Token", newToken);

            using var retryResponse = await _httpClient.SendAsync(retry, cancellationToken);
            retryResponse.EnsureSuccessStatusCode();
            return await ReadFirstItemAsync(retryResponse, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        return await ReadFirstItemAsync(response, cancellationToken);
    }

    private static async Task<ItemDto?> ReadFirstItemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<FindItemsResponse>(
            JsonOptions,
            cancellationToken);

        return result?.items?.FirstOrDefault();
    }

    private async Task<string> GetTokenAsync(
        ApiSettings settings,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_token) &&
            string.Equals(_tokenBaseUrl, settings.BaseUrl, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_tokenLogin, settings.Login, StringComparison.Ordinal) &&
            string.Equals(_tokenPassword, settings.Password, StringComparison.Ordinal))
        {
            return _token;
        }

        return await AuthenticateAsync(settings, cancellationToken);
    }

    private async Task<string> AuthenticateAsync(
        ApiSettings settings,
        CancellationToken cancellationToken,
        bool force = false)
    {
        if (!force &&
            !string.IsNullOrWhiteSpace(_token) &&
            string.Equals(_tokenBaseUrl, settings.BaseUrl, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_tokenLogin, settings.Login, StringComparison.Ordinal) &&
            string.Equals(_tokenPassword, settings.Password, StringComparison.Ordinal))
        {
            return _token;
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
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(
            JsonOptions,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(result?.token))
        {
            throw new InvalidOperationException("Datamark не вернул токен.");
        }

        _token = result.token;
        _tokenBaseUrl = settings.BaseUrl;
        _tokenLogin = settings.Login;
        _tokenPassword = settings.Password;
        return _token;
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
