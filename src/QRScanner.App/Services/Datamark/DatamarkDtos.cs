namespace QRScanner.App.Services.Datamark;

public sealed record ProductInfo(string Gtin, string? VendorCode, string? Size);

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
    public string? gtin { get; set; }

    public string? articul { get; set; }

    public List<ParamDto>? @params { get; set; }
}

internal sealed class ParamDto
{
    public string? name { get; set; }

    public string? display_value { get; set; }
}
