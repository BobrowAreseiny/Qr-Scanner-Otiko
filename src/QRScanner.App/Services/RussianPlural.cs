namespace QRScanner.App.Services;

public static class RussianPlural
{
    public static string Marks(int count)
    {
        var lastDigit = count % 10;
        var lastTwo = count % 100;

        if (lastDigit == 1 && lastTwo != 11)
        {
            return $"{count} марка";
        }

        if (lastDigit is >= 2 and <= 4 && (lastTwo < 12 || lastTwo > 14))
        {
            return $"{count} марки";
        }

        return $"{count} марок";
    }
}
