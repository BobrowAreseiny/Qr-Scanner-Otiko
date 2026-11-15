using System.Globalization;

namespace QRScanner.Core.Logging;

public static class LogFileNames
{
    public const string Prefix = "qrscanner-";
    public const string Extension = ".log";
    public const string DateFormat = "yyyy-MM-dd";

    public static string ForDate(DateTime date)
    {
        return $"{Prefix}{date.ToString(DateFormat, CultureInfo.InvariantCulture)}{Extension}";
    }

    public static bool TryParseDate(string fileName, out DateTime date)
    {
        date = default;

        if (!fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = fileName[Prefix.Length..^Extension.Length];
        return DateTime.TryParseExact(
            value,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }
}
