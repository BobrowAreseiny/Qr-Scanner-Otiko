using QRScanner.Core.Migration;

namespace QRScanner.Core.Tests;

public class LegacyMigrationMapperTests
{
    [Fact]
    public void Maps_corobs_and_barcodes()
    {
        var corobs = new[]
        {
            new LegacyCorobRecord(1, "Короб А", false),
            new LegacyCorobRecord(2, "Короб Б", true)
        };

        var barcodes = new[]
        {
            new LegacyBarcodeRecord(10, 1, " 0104600000000001 ", "4600000000001", "ART-1", "42"),
            new LegacyBarcodeRecord(11, 2, "0104600000000002", null, null, null)
        };

        var plan = LegacyMigrationMapper.Map(corobs, barcodes);

        Assert.Equal(2, plan.Corobs.Count);
        Assert.Equal(0, plan.OrphanBarcodes);

        var first = plan.Corobs[0];
        Assert.Equal("Короб А", first.Name);
        Assert.False(first.IsShipped);
        Assert.Single(first.Barcodes);
        Assert.Equal("0104600000000001", first.Barcodes[0].Barcode);
        Assert.Equal("4600000000001", first.Barcodes[0].Gtin);
        Assert.Equal("ART-1", first.Barcodes[0].Vendor);
        Assert.Equal("42", first.Barcodes[0].Size);

        var second = plan.Corobs[1];
        Assert.True(second.IsShipped);
        Assert.Single(second.Barcodes);
    }

    [Fact]
    public void Uses_default_name_for_empty_corob_name()
    {
        var plan = LegacyMigrationMapper.Map(
            new[] { new LegacyCorobRecord(7, "   ", false) },
            Array.Empty<LegacyBarcodeRecord>());

        Assert.Single(plan.Corobs);
        Assert.Equal("№7", plan.Corobs[0].Name);
    }

    [Fact]
    public void Skips_empty_barcodes_and_counts_orphans()
    {
        var corobs = new[] { new LegacyCorobRecord(1, "Короб", false) };
        var barcodes = new[]
        {
            new LegacyBarcodeRecord(1, 1, " ", null, null, null),
            new LegacyBarcodeRecord(2, 1, "VALID", null, null, null),
            new LegacyBarcodeRecord(3, 99, "ORPHAN", null, null, null)
        };

        var plan = LegacyMigrationMapper.Map(corobs, barcodes);

        Assert.Single(plan.Corobs[0].Barcodes);
        Assert.Equal("VALID", plan.Corobs[0].Barcodes[0].Barcode);
        Assert.Equal(1, plan.OrphanBarcodes);
    }
}
