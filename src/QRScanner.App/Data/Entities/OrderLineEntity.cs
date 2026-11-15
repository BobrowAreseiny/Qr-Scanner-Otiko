using SQLite;
using QRScanner.Core.Barcodes;

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
