using System.Globalization;
using System.Runtime.Versioning;
using Android.Content;
using Android.Provider;
using QRScanner.Core.Export;

namespace QRScanner.App.Services;

public sealed class OrderFileExporter : IOrderFileExporter
{
    private const string PublicRoot = "Otiko";
    private const string OrdersSubfolder = "Otiko/Заказы";
    private const string MediaIdColumn = "_id";

    public async Task<OrderFileExportResult> ExportAsync(
        string orderName,
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"{OrderExportWriter.SanitizeFileName(orderName)}.txt";

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            try
            {
                return await ExportToDownloadsAsync(fileName, codes, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось сохранить заказ в Downloads: {ex}");
            }
        }

        var directory = Path.Combine(FileSystem.AppDataDirectory, OrdersSubfolder);
        var path = await OrderExportWriter.WriteAsync(
            directory,
            orderName,
            codes,
            cancellationToken);

        return new OrderFileExportResult(
            Path.GetFileName(path),
            path,
            path,
            IsPublic: false);
    }

    public Task<ExportFileCleanupResult> CleanupOldFilesAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return Task.FromResult(new ExportFileCleanupResult(0, 0));
        }

        return Task.FromResult(CleanupDownloads(maxAge, cancellationToken));
    }

    [SupportedOSPlatform("android29.0")]
    private static ExportFileCleanupResult CleanupDownloads(
        TimeSpan maxAge,
        CancellationToken cancellationToken)
    {
        var context = Android.App.Application.Context;
        var resolver = context.ContentResolver
            ?? throw new InvalidOperationException("ContentResolver недоступен.");

        var collection = MediaStore.Downloads.ExternalContentUri;
        var threshold = DateTimeOffset.UtcNow
            .Subtract(maxAge)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var projection = new[]
        {
            MediaIdColumn,
            MediaStore.IMediaColumns.Size
        };

        var selection =
            $"{MediaStore.IMediaColumns.RelativePath} LIKE ? AND " +
            $"({MediaStore.IMediaColumns.DisplayName} LIKE ? OR " +
            $"{MediaStore.IMediaColumns.DisplayName} LIKE ?) AND " +
            $"{MediaStore.IMediaColumns.DateModified} < ?";

        var selectionArgs = new[]
        {
            $"{Android.OS.Environment.DirectoryDownloads}/{PublicRoot}/%",
            "%.txt",
            "%.png",
            threshold
        };

        var deleted = 0;
        long freedBytes = 0;

        using var cursor = resolver.Query(
            collection,
            projection,
            selection,
            selectionArgs,
            sortOrder: null);

        if (cursor is null)
        {
            return new ExportFileCleanupResult(0, 0);
        }

        var idColumn = cursor.GetColumnIndexOrThrow(MediaIdColumn);
        var sizeColumn = cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.Size);

        while (cursor.MoveToNext())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var id = cursor.GetLong(idColumn);
            var size = cursor.GetLong(sizeColumn);
            var uri = ContentUris.WithAppendedId(collection, id);

            if (resolver.Delete(uri, null, null) > 0)
            {
                deleted++;
                freedBytes += size;
            }
        }

        return new ExportFileCleanupResult(deleted, freedBytes);
    }

    [SupportedOSPlatform("android29.0")]
    private static async Task<OrderFileExportResult> ExportToDownloadsAsync(
        string fileName,
        IEnumerable<string> codes,
        CancellationToken cancellationToken)
    {
        var context = Android.App.Application.Context;
        var resolver = context.ContentResolver
            ?? throw new InvalidOperationException("ContentResolver недоступен.");

        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
        values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
        values.Put(
            MediaStore.IMediaColumns.RelativePath,
            $"{Android.OS.Environment.DirectoryDownloads}/{OrdersSubfolder}");

        var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values)
            ?? throw new InvalidOperationException(
                "Не удалось создать файл в папке Download/Otiko/Заказы.");

        await using (var stream = resolver.OpenOutputStream(uri)
            ?? throw new InvalidOperationException(
                "Не удалось открыть файл в папке Download/Otiko/Заказы."))
        {
            await OrderExportWriter.WriteAsync(stream, codes, cancellationToken);
        }

        return new OrderFileExportResult(
            fileName,
            $"Download/{OrdersSubfolder}/{fileName}",
            uri.ToString() ?? string.Empty,
            IsPublic: true);
    }
}
