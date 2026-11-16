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

        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await WriteAsync(stream, codes, cancellationToken);
        return path;
    }

    public static async Task WriteAsync(
        Stream stream,
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true)
        {
            NewLine = "\n"
        };

        foreach (var code in codes.Select(BarcodeParser.CleanForExport))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(code);
        }

        await writer.FlushAsync(cancellationToken);
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
