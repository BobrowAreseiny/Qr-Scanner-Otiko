using System.Text.Json;
using QRScanner.Core.Integration;

namespace QRScanner.App.Services;

public sealed class SecureStorageOneCSettingsService : IOneCSettingsService
{
    private const string StorageKey = "onec_settings_v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<OneCConnectionSettings> LoadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return OneCConnectionSettings.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<OneCConnectionSettings>(json, SerializerOptions);
            return (settings ?? OneCConnectionSettings.Default).Normalize();
        }
        catch (JsonException)
        {
            return OneCConnectionSettings.Default;
        }
    }

    public Task SaveAsync(OneCConnectionSettings settings)
    {
        var normalized = settings.Normalize();
        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        return SecureStorage.Default.SetAsync(StorageKey, json);
    }
}
