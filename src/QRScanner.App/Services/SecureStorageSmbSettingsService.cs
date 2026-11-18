namespace QRScanner.App.Services;

public sealed class SecureStorageSmbSettingsService : ISmbSettingsService
{
    private const string ServerKey = "smb_server_v1";
    private const string ShareKey = "smb_share_v1";
    private const string DomainKey = "smb_domain_v1";
    private const string LoginKey = "smb_login_v1";
    private const string PasswordKey = "smb_password_v1";

    public async Task<SmbConnectionSettings> LoadAsync()
    {
        var server = await SecureStorage.Default.GetAsync(ServerKey);
        var share = await SecureStorage.Default.GetAsync(ShareKey);
        var domain = await SecureStorage.Default.GetAsync(DomainKey);
        var login = await SecureStorage.Default.GetAsync(LoginKey);
        var password = await SecureStorage.Default.GetAsync(PasswordKey);

        return new SmbConnectionSettings(
            server ?? string.Empty,
            string.IsNullOrWhiteSpace(share) ? "QRScanner" : share,
            domain ?? string.Empty,
            login ?? string.Empty,
            password ?? string.Empty).Normalize();
    }

    public async Task SaveAsync(SmbConnectionSettings settings)
    {
        var normalized = settings.Normalize();

        await SecureStorage.Default.SetAsync(ServerKey, normalized.Server);
        await SecureStorage.Default.SetAsync(ShareKey, normalized.Share);
        await SecureStorage.Default.SetAsync(DomainKey, normalized.Domain);
        await SecureStorage.Default.SetAsync(LoginKey, normalized.Login);
        await SecureStorage.Default.SetAsync(PasswordKey, normalized.Password);
    }
}
