using System.Text.RegularExpressions;

namespace QRScanner.Core.Barcodes;

public static class BarcodeParser
{
    private static readonly Regex GtinPattern = new(@"01(\d{14})", RegexOptions.Compiled);

    public static string Normalize(string? code)
    {
        return code?.Trim().Trim('\r', '\n', '\0') ?? string.Empty;
    }

    public static string? ExtractGtin(string? code)
    {
        var normalized = Normalize(code);
        if (normalized.Length == 0)
        {
            return null;
        }

        var match = GtinPattern.Match(normalized);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static bool IsCorobCode(string? code)
    {
        var normalized = Normalize(code);
        return normalized.Length is > 0 and <= 10 && int.TryParse(normalized, out _);
    }
}
