using QRScanner.App.Data.Entities;
using QRScanner.Core.Integration;

namespace QRScanner.App.Services;

public static class OneCOrderRequestFactory
{
    public static OneCOrderRequest Create(
        OrderEntity order,
        IEnumerable<OrderLineEntity> lines,
        string warehouse)
    {
        return new OneCOrderRequest(
            order.ExternalId,
            order.Name,
            AsUtc(order.CreatedAtUtc),
            warehouse,
            lines
                .Select(line => new OneCOrderLine(
                    line.Barcode,
                    line.Gtin,
                    line.VendorCode,
                    line.Size,
                    line.SourceCorobId))
                .ToList());
    }

    public static string EnsureExternalId(OrderEntity order)
    {
        if (string.IsNullOrWhiteSpace(order.ExternalId))
        {
            order.ExternalId = Guid.NewGuid().ToString("D");
        }

        return order.ExternalId;
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
