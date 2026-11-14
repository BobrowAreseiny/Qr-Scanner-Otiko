using SQLite;

namespace QRScanner.App.Data.Legacy;

[Table("BoxBarcodeSQL")]
internal sealed class LegacyBarcodeRow
{
    [PrimaryKey, Column("_id")]
    public int Id { get; set; }

    public int CorobID { get; set; }

    public string? BoxBarcode { get; set; }

    public string? BarcodeNumber { get; set; }

    public string? BarcodeVendor { get; set; }

    public string? BarcodeSize { get; set; }
}
