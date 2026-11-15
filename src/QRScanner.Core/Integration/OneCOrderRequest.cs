using System.Text.Json;
using System.Text.Json.Serialization;

namespace QRScanner.Core.Integration;

public sealed record OneCOrderLine(
    string Barcode,
    string? Gtin,
    string? VendorCode,
    string? Size,
    int? SourceCorobId);

public sealed record OneCOrderRequest(
    string ExternalId,
    string Name,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAtUtc,
    string Warehouse,
    IReadOnlyList<OneCOrderLine> Lines);

public static class OneCJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeOrder(OneCOrderRequest request)
    {
        return JsonSerializer.Serialize(request, Options);
    }
}
