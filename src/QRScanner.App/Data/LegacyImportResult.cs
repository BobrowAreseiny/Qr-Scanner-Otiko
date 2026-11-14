namespace QRScanner.App.Data;

public sealed record LegacyImportResult(
    bool Imported,
    int Corobs,
    int Barcodes,
    int OrphanBarcodes,
    string? SourcePath,
    string? Error = null)
{
    public static LegacyImportResult NotNeeded { get; } = new(false, 0, 0, 0, null);

    public static LegacyImportResult AlreadyImported { get; } = new(false, 0, 0, 0, null);

    public static LegacyImportResult Failed(string path, string error) => new(false, 0, 0, 0, path, error);
}
