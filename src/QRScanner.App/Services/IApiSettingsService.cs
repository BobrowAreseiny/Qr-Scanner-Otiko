namespace QRScanner.App.Services;

public interface IApiSettingsService
{
    Task<ApiSettings> LoadAsync();

    Task SaveAsync(ApiSettings settings);
}
