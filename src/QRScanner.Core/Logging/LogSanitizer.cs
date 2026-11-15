using System.Text.RegularExpressions;

namespace QRScanner.Core.Logging;

public static class LogSanitizer
{
    private static readonly Regex AuthorizationPattern = new(
        @"(?i)(authorization\s*:\s*(?:bearer|basic)\s+)([^\s,;]+)",
        RegexOptions.Compiled);

    private static readonly Regex SecretPattern = new(
        @"(?i)\b(password|passwd|pwd|token|secret|api[_-]?key|access[_-]?key|ключ)\b(""|')?(\s*)([:=])(\s*)[^\s,;]+",
        RegexOptions.Compiled);

    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = AuthorizationPattern.Replace(text, "$1***");
        result = SecretPattern.Replace(result, "$1$2$3$4$5***");
        return result;
    }
}
