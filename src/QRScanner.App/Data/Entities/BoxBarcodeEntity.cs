using SQLite;
using QRScanner.Core.Barcodes;

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

    [Ignore]
    public string DisplayTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Gtin))
            {
                return $"GTIN {Gtin}";
            }

            var code = BarcodeParser.CleanForExport(Barcode);
            if (code.Length == 14 && code.All(char.IsDigit))
            {
                return $"GTIN {code}";
            }

            return code.Length <= 24 ? code : $"{code[..24]}…";
        }
    }

    [Ignore]
    public string ProductDetails
    {
        get
        {
            var parts = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(VendorCode))
            {
                parts.Add($"Артикул: {VendorCode}");
            }

            if (!string.IsNullOrWhiteSpace(Size))
            {
                parts.Add($"Размер: {Size}");
            }

            return string.Join(" · ", parts);
        }
    }
}
