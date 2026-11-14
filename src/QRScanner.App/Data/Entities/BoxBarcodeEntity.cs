using SQLite;

namespace QRScanner.App.Data.Entities;

[Table("BoxBarcode")]
public sealed class BoxBarcodeEntity
{
    [PrimaryKey, AutoIncrement, Column("_id")]
    public int Id { get; set; }

    [Indexed]
    public int CorobId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string? Gtin { get; set; }

    public string? VendorCode { get; set; }

    public string? Size { get; set; }

    public DateTime ScannedAtUtc { get; set; }
}
