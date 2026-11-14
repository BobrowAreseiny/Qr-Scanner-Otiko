namespace QRScanner.Core.Migration;

public static class LegacyMigrationMapper
{
    public static LegacyImportPlan Map(
        IEnumerable<LegacyCorobRecord> corobs,
        IEnumerable<LegacyBarcodeRecord> barcodes)
    {
        var corobList = corobs.OrderBy(c => c.Id).ToList();
        var knownCorobIds = corobList.Select(c => c.Id).ToHashSet();

        var groupedBarcodes = barcodes
            .Where(b => !string.IsNullOrWhiteSpace(b.Barcode))
            .GroupBy(b => b.CorobId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Id).ToList());

        var orphanBarcodes = groupedBarcodes
            .Where(pair => !knownCorobIds.Contains(pair.Key))
            .Sum(pair => pair.Value.Count);

        var importedCorobs = new List<ImportedCorob>(corobList.Count);

        foreach (var corob in corobList)
        {
            var barcodesForCorob = groupedBarcodes.TryGetValue(corob.Id, out var rows)
                ? rows.Select(MapBarcode).ToList()
                : new List<ImportedBarcode>();

            var name = string.IsNullOrWhiteSpace(corob.Name)
                ? $"№{corob.Id}"
                : corob.Name.Trim();

            importedCorobs.Add(new ImportedCorob(
                corob.Id,
                name,
                corob.IsScanned,
                barcodesForCorob));
        }

        return new LegacyImportPlan(importedCorobs, orphanBarcodes);
    }

    private static ImportedBarcode MapBarcode(LegacyBarcodeRecord row)
    {
        return new ImportedBarcode(
            row.Id,
            row.Barcode!.Trim(),
            Clean(row.Gtin),
            Clean(row.Vendor),
            Clean(row.Size));
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
