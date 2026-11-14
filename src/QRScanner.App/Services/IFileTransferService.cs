namespace QRScanner.App.Services;

public interface IFileTransferService
{
    Task<bool> SaveAsync(string sourcePath, string suggestedName, string mimeType);

    Task<string?> PickAsync(string mimeType);
}
