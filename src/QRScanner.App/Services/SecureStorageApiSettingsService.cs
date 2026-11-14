using QRScanner.Core;

namespace QRScanner.App.Services;

public sealed class SecureStorageApiSettingsService : IApiSettingsService
{
    private const string BaseUrlKey = "api_base_url_v1";
    private const string LoginKey = "api_login_v1";
    private const string PasswordKey = "api_password_v1";

    public async Task<ApiSettings> LoadAsync()
    {
        var baseUrl = await SecureStorage.Default.GetAsync(BaseUrlKey);
        var login = await SecureStorage.Default.GetAsync(LoginKey);
        var password = await SecureStorage.Default.GetAsync(PasswordKey);

        return new ApiSettings(
            string.IsNullOrWhiteSpace(baseUrl) ? AppConstants.ApiBaseUrl : baseUrl,
            login ?? string.Empty,
            password ?? string.Empty);
    }

    public async Task SaveAsync(ApiSettings settings)
    {
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? AppConstants.ApiBaseUrl
            : settings.BaseUrl.Trim();

        await SecureStorage.Default.SetAsync(BaseUrlKey, baseUrl);
        await SecureStorage.Default.SetAsync(LoginKey, settings.Login.Trim());
        await SecureStorage.Default.SetAsync(PasswordKey, settings.Password);
    }
}
