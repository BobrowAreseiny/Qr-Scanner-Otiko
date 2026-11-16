using System.Text;
using QRScanner.Core.Barcodes;

namespace QRScanner.Core.Export;

public static class OrderExportWriter
{
    public static async Task<string> WriteAsync(
        string directory,
        string fileName,
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);

        var safeName = SanitizeFileName(fileName);
        var path = Path.Combine(directory, $"{safeName}.txt");

        await using var writer = new StreamWriter(
            path,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            NewLine = "\n"
        };

        foreach (var code in codes.Select(BarcodeParser.CleanForExport))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(code);
        }

        return path;
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (safe.Length > 80)
        {
            safe = safe[..80].Trim();
        }

        return string.IsNullOrWhiteSpace(safe) ? "Заказ" : safe;
    }
}
