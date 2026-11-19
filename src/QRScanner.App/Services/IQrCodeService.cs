namespace QRScanner.App.Services;

public interface IQrCodeService
{
    Task<string> GenerateAsync(int corobId, string corobName);

    Task<QrFileSaveResult> GetOrCreateAsync(
        int corobId,
        string corobName,
        CancellationToken cancellationToken = default);

    Task<QrFileSaveResult> SaveToDownloadsAsync(
        int corobId,
        string corobName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int corobId,
        string corobName,
        CancellationToken cancellationToken = default);
}

public sealed record QrFileSaveResult(
    string FileName,
    string DisplayPath,
    string Location,
    bool IsPublic,
    bool Created = true);
