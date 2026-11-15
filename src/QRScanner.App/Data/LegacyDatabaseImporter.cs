using Microsoft.Extensions.Logging;
using QRScanner.App.Data.Entities;
using QRScanner.App.Data.Legacy;
using QRScanner.Core;
using QRScanner.Core.Migration;
using SQLite;

namespace QRScanner.App.Data;

public sealed class LegacyDatabaseImporter
{
    private readonly ILogger<LegacyDatabaseImporter> _logger;

    public LegacyDatabaseImporter(ILogger<LegacyDatabaseImporter> logger)
    {
        _logger = logger;
    }

    public async Task<LegacyImportResult> ImportIfNeededAsync(SQLiteAsyncConnection target)
    {
        var sourcePath = FindLegacyDatabase();
        if (sourcePath is null)
        {
            return LegacyImportResult.NotNeeded;
        }

        return await ImportFromFileAsync(sourcePath, target);
    }

    public async Task<LegacyImportResult> ImportFromFileAsync(
        string sourcePath,
        SQLiteAsyncConnection target)
    {
        if (!File.Exists(sourcePath))
        {
            return LegacyImportResult.Failed(sourcePath, "Файл старой базы не найден.");
        }

        if (await target.Table<CorobEntity>().CountAsync() > 0)
        {
            return LegacyImportResult.AlreadyImported;
        }

        LegacyImportPlan plan;
        try
        {
            var options = new SQLiteConnectionString(
                sourcePath,
                storeDateTimeAsTicks: true,
                key: null,
                openFlags: SQLiteOpenFlags.ReadOnly);

            using var legacy = new SQLiteConnection(options);
            var corobs = legacy.Table<LegacyCorobRow>().ToList();
            var barcodes = legacy.Table<LegacyBarcodeRow>().ToList();

            plan = LegacyMigrationMapper.Map(
                corobs.Select(c => new LegacyCorobRecord(c.Id, c.CorobName, c.IsScanned)),
                barcodes.Select(b => new LegacyBarcodeRecord(
                    b.Id,
                    b.CorobID,
                    b.BoxBarcode,
                    b.BarcodeNumber,
                    b.BarcodeVendor,
                    b.BarcodeSize)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось прочитать старую базу {Path}", sourcePath);
            return LegacyImportResult.Failed(sourcePath, ex.Message);
        }

        var barcodeCount = plan.Corobs.Sum(c => c.Barcodes.Count);
        if (plan.Corobs.Count == 0 && barcodeCount == 0)
        {
            _logger.LogInformation(
                "Старая база {Path} пуста; миграция не требуется. Пропущено марок без короба: {Orphans}",
                sourcePath,
                plan.OrphanBarcodes);
            return new LegacyImportResult(false, 0, 0, plan.OrphanBarcodes, sourcePath);
        }

        await target.RunInTransactionAsync(connection =>
        {
            foreach (var importedCorob in plan.Corobs)
            {
                var corob = new CorobEntity
                {
                    Name = importedCorob.Name,
                    // В старом приложении ёмкости не было: короба содержат 63–104 марки.
                    // 0 — стандартный режим без ограничения количества.
                    Capacity = 0,
                    IsShipped = importedCorob.IsShipped,
                    CreatedAtUtc = DateTime.UtcNow
                };

                connection.Insert(corob);

                foreach (var importedBarcode in importedCorob.Barcodes)
                {
                    connection.Insert(new BoxBarcodeEntity
                    {
                        CorobId = corob.Id,
                        Barcode = importedBarcode.Barcode,
                        Gtin = importedBarcode.Gtin,
                        VendorCode = importedBarcode.Vendor,
                        Size = importedBarcode.Size,
                        ScannedAtUtc = DateTime.UtcNow
                    });
                }
            }
        });

        _logger.LogInformation(
            "Импортировано из {Path}: коробов {Corobs}, марок {Barcodes}, пропущено марок без короба {Orphans}",
            sourcePath,
            plan.Corobs.Count,
            barcodeCount,
            plan.OrphanBarcodes);

        return new LegacyImportResult(
            true,
            plan.Corobs.Count,
            barcodeCount,
            plan.OrphanBarcodes,
            sourcePath);
    }

    public string? FindLegacyDatabase()
    {
        foreach (var path in GetCandidatePaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        var appData = FileSystem.AppDataDirectory;

        yield return Path.Combine(appData, AppConstants.LegacyDatabaseFileName);
        yield return Path.Combine(appData, ".local", "share", AppConstants.LegacyDatabaseFileName);
    }
}
