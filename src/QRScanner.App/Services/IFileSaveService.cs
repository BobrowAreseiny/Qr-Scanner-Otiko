namespace QRScanner.App.Services;

public interface IFileSaveService
{
    Task<bool> SaveAsync(string sourcePath, string suggestedName, string mimeType);
}
