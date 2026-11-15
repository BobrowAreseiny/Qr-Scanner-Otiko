using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QRScanner.Core.Integration;

namespace QRScanner.App.Services;

public sealed record OneCResult(
    bool Success,
    bool AlreadyExists,
    int? StatusCode,
    string? Number,
    string Message);

public interface IOneCApiClient
{
    Task<OneCResult> TestConnectionAsync(
        OneCConnectionSettings settings,
        CancellationToken cancellationToken = default);

    Task<OneCResult> SendOrderAsync(
        OneCConnectionSettings settings,
        OneCOrderRequest order,
        CancellationToken cancellationToken = default);
}

public sealed class OneCApiClient : IOneCApiClient
{
    public async Task<OneCResult> TestConnectionAsync(
        OneCConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = settings.Normalize();
        var errors = normalized.Validate();
        if (errors.Count > 0)
        {
            return Failure(string.Join(" ", errors));
        }

        try
        {
            using var client = CreateClient(normalized);
            using var response = await client.GetAsync(normalized.HealthUrl, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return new OneCResult(
                    true,
                    false,
                    statusCode,
                    null,
                    "Подключение к 1С установлено.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new OneCResult(
                    false,
                    false,
                    statusCode,
                    null,
                    $"Сервер доступен, но авторизация не прошла (HTTP {statusCode}).");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new OneCResult(
                    false,
                    false,
                    statusCode,
                    null,
                    "Сервер доступен, но адрес проверки не найден (HTTP 404).");
            }

            return new OneCResult(
                false,
                false,
                statusCode,
                null,
                $"Сервер вернул HTTP {statusCode}.{BuildSnippet(body)}");
        }
        catch (TaskCanceledException)
        {
            return Failure("Превышено время ожидания ответа 1С.");
        }
        catch (HttpRequestException ex)
        {
            return Failure($"Ошибка соединения с 1С: {ex.Message}");
        }
    }

    public async Task<OneCResult> SendOrderAsync(
        OneCConnectionSettings settings,
        OneCOrderRequest order,
        CancellationToken cancellationToken = default)
    {
        var normalized = settings.Normalize();
        var errors = normalized.Validate();
        if (errors.Count > 0)
        {
            return Failure(string.Join(" ", errors));
        }

        try
        {
            using var client = CreateClient(normalized);
            var json = OneCJson.SerializeOrder(order);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(normalized.OrdersUrl, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;
            var number = ExtractNumber(body);

            if (response.IsSuccessStatusCode)
            {
                return new OneCResult(
                    true,
                    false,
                    statusCode,
                    number,
                    string.IsNullOrWhiteSpace(number)
                        ? "Заказ отправлен в 1С."
                        : $"Заказ отправлен в 1С, номер {number}.");
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new OneCResult(
                    true,
                    true,
                    statusCode,
                    number,
                    string.IsNullOrWhiteSpace(number)
                        ? "Заказ уже был принят 1С ранее."
                        : $"Заказ уже был принят 1С, номер {number}.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new OneCResult(
                    false,
                    false,
                    statusCode,
                    null,
                    $"Авторизация в 1С не прошла (HTTP {statusCode}).");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new OneCResult(
                    false,
                    false,
                    statusCode,
                    null,
                    $"1С отклонила заказ (HTTP 400).{BuildSnippet(body)}");
            }

            return new OneCResult(
                false,
                false,
                statusCode,
                null,
                $"Ошибка 1С: HTTP {statusCode}.{BuildSnippet(body)}");
        }
        catch (TaskCanceledException)
        {
            return Failure("Превышено время ожидания ответа 1С.");
        }
        catch (HttpRequestException ex)
        {
            return Failure($"Ошибка соединения с 1С: {ex.Message}");
        }
    }

    private static HttpClient CreateClient(OneCConnectionSettings settings)
    {
        var handler = new HttpClientHandler();

        if (settings.AllowInvalidCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };

        if (settings.AuthType == OneCAuthType.Bearer)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.Token);
        }
        else if (settings.AuthType == OneCAuthType.Basic)
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{settings.Login}:{settings.Password}"));

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }

    private static string? ExtractNumber(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            foreach (var name in new[] { "number", "orderNumber", "docNumber", "Номер", "номер" })
            {
                if (root.TryGetProperty(name, out var property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ответ не JSON — номер заказа не извлекается.
        }

        return null;
    }

    private static string BuildSnippet(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var text = body.Trim().Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 300)
        {
            text = text[..300] + "…";
        }

        return $" Ответ: {text}";
    }

    private static OneCResult Failure(string message)
    {
        return new OneCResult(false, false, null, null, message);
    }
}
