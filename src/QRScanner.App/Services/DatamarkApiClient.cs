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

    public async Task<LabelCheckResult?> CheckLabelAsync(
        string code,
        ApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = BarcodeParser.CleanForExport(code);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var token = await GetTokenAsync(settings, cancellationToken);
        var uri = BuildUri(settings.BaseUrl, "v2/labels");

        using var response = await SendLabelsAsync(uri, normalized, token, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var newToken = await AuthenticateAsync(settings, cancellationToken, force: true);
            using var retryResponse = await SendLabelsAsync(
                uri,
                normalized,
                newToken,
                cancellationToken);
            return await ReadLabelResponseAsync(
                retryResponse,
                normalized,
                cancellationToken);
        }

        return await ReadLabelResponseAsync(response, normalized, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendLabelsAsync(
        Uri uri,
        string code,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new { labels = new[] { code } })
        };
        request.Headers.TryAddWithoutValidation("Token", token);

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<LabelCheckResult?> ReadLabelResponseAsync(
        HttpResponseMessage response,
        string code,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new LabelCheckResult(code, false, null, null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiErrorAsync(response, cancellationToken);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new LabelCheckResult(code, false, null, null, null);
        }

        using var document = JsonDocument.Parse(json);
        var item = FindFirstItem(document.RootElement);
        if (item is null)
        {
            return new LabelCheckResult(code, false, null, null, null);
        }

        var value = item.Value;
        var returnedCode = GetString(
            value,
            "label",
            "code",
            "marking_code",
            "markingCode",
            "km",
            "sgtin") ?? code;
        var found = GetBool(value, "found", "is_found", "isFound") ?? true;
        var status = GetString(value, "status", "state", "status_name", "statusName");
        var isMy = GetBool(value, "is_my", "isMy");
        var type = GetString(value, "type", "code_type", "codeType");

        var shipment = GetObject(
            value,
            "ship",
            "shipment",
            "shipment_info",
            "shipmentInfo",
            "document");
        var shipmentNumber = shipment is null
            ? null
            : GetString(
                shipment.Value,
                "number",
                "ship_number",
                "shipNumber",
                "document_number",
                "documentNumber",
                "id");
        var shipmentDate = shipment is null
            ? null
            : GetString(
                shipment.Value,
                "date",
                "ship_date",
                "shipDate",
                "shipped_at",
                "shippedAt",
                "created_at",
                "createdAt");
        var recipient = shipment is null
            ? null
            : GetString(
                shipment.Value,
                "recipient",
                "consignee",
                "receiver",
                "counterparty");

        return new LabelCheckResult(
            returnedCode,
            found,
            status,
            isMy,
            type,
            shipmentNumber,
            shipmentDate,
            recipient);
    }

    private static JsonElement? FindFirstItem(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.GetArrayLength() > 0 ? root[0] : null;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in new[] { "labels", "items", "data", "results", "result" })
        {
            if (!TryGetProperty(root, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.GetArrayLength() > 0 ? value[0] : null;
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                return value;
            }
        }

        return root.TryGetProperty("status", out _) || root.TryGetProperty("found", out _)
            ? root
            : null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                continue;
            }

            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static bool? GetBool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String &&
                bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var number))
            {
                return number != 0;
            }
        }

        return null;
    }

    private static JsonElement? GetObject(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) &&
                value.ValueKind == JsonValueKind.Object)
            {
                return value;
            }
        }

        return null;
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
            { new StringContent(settings.Password.Trim()), "password" }
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
