using SQLite;

namespace QRScanner.App.Data.Entities;

[Table("OrderLine")]
public sealed class OrderLineEntity
{
    [PrimaryKey, AutoIncrement, Column("_id")]
    public int Id { get; set; }

    [Indexed]
    public int OrderId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string? Gtin { get; set; }

    public string? VendorCode { get; set; }

    public string? Size { get; set; }

    public int? SourceCorobId { get; set; }

    public DateTime AddedAtUtc { get; set; }

    [Ignore]
    public string SourceText => SourceCorobId.HasValue
        ? $"Короб №{SourceCorobId.Value}"
        : "Отдельная марка";
}
