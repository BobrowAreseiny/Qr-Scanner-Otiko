namespace QRScanner.Core.Security;

public static class DatabaseKey
{
    public const int KeySizeBytes = 32;

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(key.Trim()).Length == KeySizeBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Normalize(string key)
    {
        return key.Trim();
    }
}
