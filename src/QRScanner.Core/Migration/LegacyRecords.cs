namespace QRScanner.Core.Migration;

public sealed record LegacyCorobRecord(int Id, string? Name, bool IsScanned);

public sealed record LegacyBarcodeRecord(
    int Id,
    int CorobId,
    string? Barcode,
    string? Gtin,
    string? Vendor,
    string? Size);

public sealed record ImportedBarcode(
    int LegacyId,
    string Barcode,
    string? Gtin,
    string? Vendor,
    string? Size);

public sealed record ImportedCorob(
    int LegacyId,
    string Name,
    bool IsShipped,
    IReadOnlyList<ImportedBarcode> Barcodes);

public sealed record LegacyImportPlan(
    IReadOnlyList<ImportedCorob> Corobs,
    int OrphanBarcodes);
