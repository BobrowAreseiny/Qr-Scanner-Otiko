namespace QRScanner.App.Services.Datamark;

public sealed record ProductInfo(
    string Gtin,
    string? VendorCode,
    string? Size,
    string? Name = null,
    bool? GtinCheck = null,
    string? Group = null,
    bool IsMy = false);

internal sealed class AuthResponse
{
    public string? token { get; set; }
}

internal sealed class FindItemsResponse
{
    public List<ItemDto>? items { get; set; }
}

internal sealed class ItemDto
{
    public string? name { get; set; }

    public string? gtin { get; set; }

    public bool? gtin_check { get; set; }

    public string? group { get; set; }

    public bool? is_my { get; set; }

    public string? articul { get; set; }

    public List<ParamDto>? @params { get; set; }
}

internal sealed class ParamDto
{
    public string? name { get; set; }

    public string? value { get; set; }

    public string? display_value { get; set; }
}

public sealed class DatamarkApiException : Exception
{
    public DatamarkApiException(
        int statusCode,
        string message,
        IReadOnlyList<string>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Details = details ?? [];
    }

    public int StatusCode { get; }

    public IReadOnlyList<string> Details { get; }
}
