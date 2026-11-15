using Microsoft.Extensions.Logging;
using QRScanner.App.Data.Entities;
using QRScanner.Core;
using SQLite;

namespace QRScanner.App.Data;

public interface IDatabaseService
{
    string DatabasePath { get; }

    Task InitializeAsync();

    Task<IReadOnlyList<CorobEntity>> GetCorobsAsync();

    Task<CorobEntity?> GetCorobAsync(int id);

    Task<IReadOnlyList<BoxBarcodeEntity>> GetBarcodesAsync(int corobId);

    Task<int> CountCorobsAsync();

    Task<int> CountBarcodesAsync();

    Task<int> GetNextCorobNumberAsync();

    Task<BoxBarcodeEntity?> FindBarcodeAsync(string barcode);

    Task<IReadOnlyList<OrderEntity>> GetOrdersAsync();

    Task<OrderEntity?> GetOrderAsync(int id);

    Task<IReadOnlyList<OrderLineEntity>> GetOrderLinesAsync(int orderId);

    Task<int> CountOrdersAsync();

    Task<OrderEntity> SaveOrderAsync(OrderEntity order);

    Task<OrderLineEntity> SaveOrderLineAsync(OrderLineEntity line);

    Task DeleteOrderAsync(int id);

    Task DeleteOrderLineAsync(int id);

    Task<CorobEntity> SaveCorobAsync(CorobEntity corob);

    Task<BoxBarcodeEntity> SaveBarcodeAsync(BoxBarcodeEntity barcode);

    Task DeleteCorobAsync(int id);

    Task DeleteBarcodeAsync(int id);

    Task DeleteShippedCorobsAsync();
}

public sealed class DatabaseService : IDatabaseService
{
    private readonly IDbKeyProvider _keyProvider;
    private readonly LegacyDatabaseImporter _legacyImporter;
    private readonly ILogger<DatabaseService> _logger;
    private readonly SemaphoreSlim _initGate = new(1, 1);

    private SQLiteAsyncConnection? _connection;
    private bool _initialized;

    public DatabaseService(
        IDbKeyProvider keyProvider,
        LegacyDatabaseImporter legacyImporter,
        ILogger<DatabaseService> logger)
    {
        _keyProvider = keyProvider;
        _legacyImporter = legacyImporter;
        _logger = logger;
    }

    public string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, AppConstants.DatabaseFileName);

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initGate.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();

            var key = await _keyProvider.GetOrCreateKeyAsync();
            var connectionString = new SQLiteConnectionString(
                DatabasePath,
                storeDateTimeAsTicks: true,
                key: key,
                openFlags: SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            var connection = new SQLiteAsyncConnection(connectionString);

            await connection.CreateTableAsync<CorobEntity>();
            await connection.CreateTableAsync<BoxBarcodeEntity>();
            await connection.CreateTableAsync<OrderEntity>();
            await connection.CreateTableAsync<OrderLineEntity>();

            var cipherVersion = await connection.ExecuteScalarAsync<string>("PRAGMA cipher_version;");
            if (string.IsNullOrWhiteSpace(cipherVersion))
            {
                throw new InvalidOperationException(
                    "SQLCipher не активен. База данных не будет открыта без шифрования.");
            }

            _logger.LogInformation("SQLCipher активен, версия: {Version}", cipherVersion);
            Console.WriteLine($"QRSCANNER_DB: SQLCipher {cipherVersion}, path={DatabasePath}");

            var importResult = await _legacyImporter.ImportIfNeededAsync(connection);
            _logger.LogInformation(
                "Импорт старой базы: imported={Imported}, corobs={Corobs}, barcodes={Barcodes}, orphans={Orphans}, source={Source}, error={Error}",
                importResult.Imported,
                importResult.Corobs,
                importResult.Barcodes,
                importResult.OrphanBarcodes,
                importResult.SourcePath,
                importResult.Error);
            Console.WriteLine(
                $"QRSCANNER_DB_IMPORT: imported={importResult.Imported}, corobs={importResult.Corobs}, barcodes={importResult.Barcodes}, orphans={importResult.OrphanBarcodes}, source={importResult.SourcePath}, error={importResult.Error}");

            _connection = connection;
            _initialized = true;

            _logger.LogInformation(
                "База данных готова: {Path}, коробов {Corobs}, марок {Barcodes}",
                DatabasePath,
                await connection.Table<CorobEntity>().CountAsync(),
                await connection.Table<BoxBarcodeEntity>().CountAsync());
            Console.WriteLine(
                $"QRSCANNER_DB_READY: corobs={await connection.Table<CorobEntity>().CountAsync()}, barcodes={await connection.Table<BoxBarcodeEntity>().CountAsync()}");
        }
        finally
        {
            _initGate.Release();
        }
    }

    public async Task<IReadOnlyList<CorobEntity>> GetCorobsAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<CorobEntity>()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<CorobEntity?> GetCorobAsync(int id)
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<CorobEntity>()
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<BoxBarcodeEntity>> GetBarcodesAsync(int corobId)
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<BoxBarcodeEntity>()
            .Where(b => b.CorobId == corobId)
            .OrderBy(b => b.Id)
            .ToListAsync();
    }

    public async Task<int> CountCorobsAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<CorobEntity>().CountAsync();
    }

    public async Task<int> CountBarcodesAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<BoxBarcodeEntity>().CountAsync();
    }

    public async Task<int> GetNextCorobNumberAsync()
    {
        var connection = await GetConnectionAsync();
        var last = await connection.Table<CorobEntity>()
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();

        return (last?.Id ?? 0) + 1;
    }

    public async Task<BoxBarcodeEntity?> FindBarcodeAsync(string barcode)
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<BoxBarcodeEntity>()
            .Where(b => b.Barcode == barcode)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<OrderEntity>> GetOrdersAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<OrderEntity>()
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<OrderEntity?> GetOrderAsync(int id)
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<OrderEntity>()
            .Where(o => o.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<OrderLineEntity>> GetOrderLinesAsync(int orderId)
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<OrderLineEntity>()
            .Where(l => l.OrderId == orderId)
            .OrderBy(l => l.Id)
            .ToListAsync();
    }

    public async Task<int> CountOrdersAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<OrderEntity>().CountAsync();
    }

    public async Task<OrderEntity> SaveOrderAsync(OrderEntity order)
    {
        var connection = await GetConnectionAsync();

        if (order.CreatedAtUtc == default)
        {
            order.CreatedAtUtc = DateTime.UtcNow;
        }

        if (order.Id == 0)
        {
            await connection.InsertAsync(order);
        }
        else
        {
            await connection.UpdateAsync(order);
        }

        return order;
    }

    public async Task<OrderLineEntity> SaveOrderLineAsync(OrderLineEntity line)
    {
        var connection = await GetConnectionAsync();

        if (line.AddedAtUtc == default)
        {
            line.AddedAtUtc = DateTime.UtcNow;
        }

        if (line.Id == 0)
        {
            await connection.InsertAsync(line);
        }
        else
        {
            await connection.UpdateAsync(line);
        }

        return line;
    }

    public async Task DeleteOrderAsync(int id)
    {
        var connection = await GetConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM OrderLine WHERE OrderId = ?", id);
        await connection.DeleteAsync<OrderEntity>(id);
    }

    public async Task DeleteOrderLineAsync(int id)
    {
        var connection = await GetConnectionAsync();
        await connection.DeleteAsync<OrderLineEntity>(id);
    }

    public async Task<CorobEntity> SaveCorobAsync(CorobEntity corob)
    {
        var connection = await GetConnectionAsync();

        if (corob.CreatedAtUtc == default)
        {
            corob.CreatedAtUtc = DateTime.UtcNow;
        }

        if (corob.Id == 0)
        {
            await connection.InsertAsync(corob);
        }
        else
        {
            await connection.UpdateAsync(corob);
        }

        return corob;
    }

    public async Task<BoxBarcodeEntity> SaveBarcodeAsync(BoxBarcodeEntity barcode)
    {
        var connection = await GetConnectionAsync();

        if (barcode.ScannedAtUtc == default)
        {
            barcode.ScannedAtUtc = DateTime.UtcNow;
        }

        if (barcode.Id == 0)
        {
            await connection.InsertAsync(barcode);
        }
        else
        {
            await connection.UpdateAsync(barcode);
        }

        return barcode;
    }

    public async Task DeleteCorobAsync(int id)
    {
        var connection = await GetConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM BoxBarcode WHERE CorobId = ?", id);
        await connection.DeleteAsync<CorobEntity>(id);
    }

    public async Task DeleteBarcodeAsync(int id)
    {
        var connection = await GetConnectionAsync();
        await connection.DeleteAsync<BoxBarcodeEntity>(id);
    }

    public async Task DeleteShippedCorobsAsync()
    {
        var connection = await GetConnectionAsync();
        var shipped = await connection.Table<CorobEntity>()
            .Where(c => c.IsShipped)
            .ToListAsync();

        foreach (var corob in shipped)
        {
            await DeleteCorobAsync(corob.Id);
        }
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        await InitializeAsync();
        return _connection ?? throw new InvalidOperationException("База данных не инициализирована.");
    }
}
