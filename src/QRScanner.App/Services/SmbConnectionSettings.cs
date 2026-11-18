namespace QRScanner.App.Services;

public sealed record SmbConnectionSettings(
    string Server,
    string Share,
    string Domain,
    string Login,
    string Password)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Server) &&
        !string.IsNullOrWhiteSpace(Share) &&
        !string.IsNullOrWhiteSpace(Login) &&
        !string.IsNullOrWhiteSpace(Password);

    public SmbConnectionSettings Normalize()
    {
        var server = (Server ?? string.Empty).Trim();
        if (server.StartsWith(@"\\", StringComparison.Ordinal))
        {
            server = server[2..];
        }

        server = server.TrimEnd('\\', '/');

        var share = (Share ?? string.Empty).Trim().Trim('\\', '/');
        var domain = (Domain ?? string.Empty).Trim();
        var login = (Login ?? string.Empty).Trim();

        var separator = login.IndexOf('\\');
        if (separator > 0)
        {
            domain = login[..separator].Trim();
            login = login[(separator + 1)..].Trim();
        }

        return new SmbConnectionSettings(
            server,
            share,
            domain,
            login,
            Password ?? string.Empty);
    }
}

public interface ISmbSettingsService
{
    Task<SmbConnectionSettings> LoadAsync();

    Task SaveAsync(SmbConnectionSettings settings);
}
