namespace QRScanner.App.Services;

public sealed record ApiSettings(string BaseUrl, string Login, string Password)
{
    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Login) &&
        !string.IsNullOrWhiteSpace(Password);
}
