namespace QRScanner.App.Data;

public interface IDbKeyProvider
{
    Task<string> GetOrCreateKeyAsync();

    Task<string?> GetKeyAsync();

    Task SetKeyAsync(string key);
}
