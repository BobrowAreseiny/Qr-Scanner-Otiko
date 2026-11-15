using System.Text.Json;
using QRScanner.Core.Integration;

namespace QRScanner.Core.Tests;

public sealed class OneCJsonTests
{
    [Fact]
    public void SerializeOrder_UsesCamelCaseAndOmitsNulls()
    {
        var request = new OneCOrderRequest(
            "6f1c2a7e-9b4d-4f0e-a1c2-3d4e5f607182",
            "Заказ 15.11.2026 10-30",
            new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
            "Основной склад",
            [
                new OneCOrderLine(
                    "0104810498044822212UJogcqL51WQE91000592ABC",
                    "04810498044822",
                    "25C1",
                    "37",
                    1),
                new OneCOrderLine("ABC-123", null, null, null, null)
            ]);

        var json = OneCJson.SerializeOrder(request);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("6f1c2a7e-9b4d-4f0e-a1c2-3d4e5f607182", root.GetProperty("externalId").GetString());
        Assert.Equal("Заказ 15.11.2026 10-30", root.GetProperty("name").GetString());
        Assert.Equal("Основной склад", root.GetProperty("warehouse").GetString());
        Assert.StartsWith("2026-11-15T10:30:00", root.GetProperty("createdAt").GetString());

        var lines = root.GetProperty("lines");
        Assert.Equal(2, lines.GetArrayLength());

        var first = lines[0];
        Assert.Equal("04810498044822", first.GetProperty("gtin").GetString());
        Assert.Equal("25C1", first.GetProperty("vendorCode").GetString());
        Assert.Equal("37", first.GetProperty("size").GetString());
        Assert.Equal(1, first.GetProperty("sourceCorobId").GetInt32());

        var second = lines[1];
        Assert.False(second.TryGetProperty("gtin", out _));
        Assert.False(second.TryGetProperty("vendorCode", out _));
        Assert.False(second.TryGetProperty("size", out _));
        Assert.False(second.TryGetProperty("sourceCorobId", out _));
    }
}
