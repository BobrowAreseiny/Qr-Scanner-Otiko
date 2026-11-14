using System.Security.Cryptography;

namespace QRScanner.App.Data;

public sealed class SecureStorageDbKeyProvider : IDbKeyProvider
{
    private const string StorageKey = "db_key_v1";

    public async Task<string> GetOrCreateKeyAsync()
    {
        var existing = await SecureStorage.Default.GetAsync(StorageKey);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await SecureStorage.Default.SetAsync(StorageKey, key);
        return key;
    }

    public Task<string?> GetKeyAsync()
    {
        return SecureStorage.Default.GetAsync(StorageKey);
    }

    public Task SetKeyAsync(string key)
    {
        return SecureStorage.Default.SetAsync(StorageKey, key);
    }
}
