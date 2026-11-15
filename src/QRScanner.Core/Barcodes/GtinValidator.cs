namespace QRScanner.Core.Barcodes;

public static class GtinValidator
{
    private static readonly int[] ValidLengths = [8, 12, 13, 14];

    public static bool IsValid(string? gtin)
    {
        var value = Normalize(gtin);
        if (value is null)
        {
            return false;
        }

        var sum = 0;
        var weight = 3;

        for (var index = value.Length - 2; index >= 0; index--)
        {
            if (!char.IsDigit(value[index]))
            {
                return false;
            }

            sum += (value[index] - '0') * weight;
            weight = weight == 3 ? 1 : 3;
        }

        var expected = (10 - (sum % 10)) % 10;
        return value[^1] - '0' == expected;
    }

    public static string? Normalize(string? gtin)
    {
        if (string.IsNullOrWhiteSpace(gtin))
        {
            return null;
        }

        var value = gtin.Trim();
        if (!value.All(char.IsDigit) || !ValidLengths.Contains(value.Length))
        {
            return null;
        }

        return value.PadLeft(14, '0');
    }
}
