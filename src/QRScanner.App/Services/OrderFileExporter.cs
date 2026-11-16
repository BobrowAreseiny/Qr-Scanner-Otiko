using System.Runtime.Versioning;
using Android.Content;
using Android.Provider;
using QRScanner.Core.Export;

namespace QRScanner.App.Services;

public sealed class OrderFileExporter : IOrderFileExporter
{
    private const string PublicSubfolder = "Otiko";

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

        var directory = Path.Combine(FileSystem.AppDataDirectory, PublicSubfolder);
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
            $"{Android.OS.Environment.DirectoryDownloads}/{PublicSubfolder}");

        var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values)
            ?? throw new InvalidOperationException(
                "Не удалось создать файл в папке Download/Otiko.");

        await using (var stream = resolver.OpenOutputStream(uri)
            ?? throw new InvalidOperationException(
                "Не удалось открыть файл в папке Download/Otiko."))
        {
            await OrderExportWriter.WriteAsync(stream, codes, cancellationToken);
        }

        return new OrderFileExportResult(
            fileName,
            $"Download/{PublicSubfolder}/{fileName}",
            uri.ToString() ?? string.Empty,
            IsPublic: true);
    }
}
