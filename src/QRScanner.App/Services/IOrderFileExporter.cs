namespace QRScanner.App.Services;

public interface IOrderFileExporter
{
    Task<OrderFileExportResult> ExportAsync(
        string orderName,
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default);

    Task<ExportFileCleanupResult> CleanupOldFilesAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default);
}

public sealed record OrderFileExportResult(
    string FileName,
    string DisplayPath,
    string Location,
    bool IsPublic);

public sealed record ExportFileCleanupResult(
    int DeletedCount,
    long FreedBytes);
