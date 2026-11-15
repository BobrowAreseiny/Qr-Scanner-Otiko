using QRCoder;
using SkiaSharp;

namespace QRScanner.App.Services;

public sealed class QrCodeService : IQrCodeService
{
    public async Task<string> GenerateAsync(int corobId, string corobName)
    {
        var pngBytes = await Task.Run(() =>
        {
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(
                corobId.ToString(),
                QRCodeGenerator.ECCLevel.L);

            var qrCode = new PngByteQRCode(data);
            var rawQr = qrCode.GetGraphic(20);
            return AddText(rawQr, corobName);
        });

        var directory = Path.Combine(FileSystem.CacheDirectory, "qr");
        Directory.CreateDirectory(directory);

        foreach (var oldFile in Directory.EnumerateFiles(directory, $"{corobId}-*.png"))
        {
            try
            {
                File.Delete(oldFile);
            }
            catch
            {
                // Старый файл не критичен: новый будет записан отдельно.
            }
        }

        var safeName = SanitizeFileName(corobName);
        var path = Path.Combine(directory, $"{corobId}-{safeName}.png");
        await File.WriteAllBytesAsync(path, pngBytes);
        return path;
    }

    private static byte[] AddText(byte[] qrBytes, string text)
    {
        using var original = SKBitmap.Decode(qrBytes)
            ?? throw new InvalidOperationException("Не удалось декодировать QR-код.");

        var textSize = Math.Max(original.Width * 0.06f, 26f);
        var padding = (int)(textSize / 2);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = textSize,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        var maxWidth = original.Width - padding * 2;
        var lines = WrapText(text, paint, maxWidth, maxLines: 3);
        var lineHeight = textSize * 1.25f;
        var width = original.Width;
        var height = original.Height + padding * 2 + (int)(lineHeight * lines.Count);
        var info = new SKImageInfo(width, height);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(original, 0, 0);

        var metrics = paint.FontMetrics;
        for (var index = 0; index < lines.Count; index++)
        {
            var baseline = original.Height + padding - metrics.Ascent + index * lineHeight;
            canvas.DrawText(lines[index], width / 2f, baseline, paint);
        }

        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static List<string> WrapText(
        string text,
        SKPaint paint,
        float maxWidth,
        int maxLines)
    {
        var normalized = string.Join(
            " ",
            (text ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Короб";
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>(maxLines);
        var current = string.Empty;
        var consumedAll = true;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (paint.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
                current = string.Empty;
                if (lines.Count == maxLines)
                {
                    consumedAll = false;
                    break;
                }
            }

            if (paint.MeasureText(word) <= maxWidth)
            {
                current = word;
                continue;
            }

            lines.Add(TruncateToWidth(word, paint, maxWidth));
            if (lines.Count == maxLines)
            {
                current = string.Empty;
                consumedAll = false;
                break;
            }
        }

        if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        if (lines.Count == 0)
        {
            lines.Add(TruncateToWidth(normalized, paint, maxWidth));
        }

        if (!consumedAll && lines.Count > 0 && !lines[^1].EndsWith('…'))
        {
            lines[^1] = AppendEllipsis(lines[^1], paint, maxWidth);
        }

        return lines;
    }

    private static string TruncateToWidth(string value, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(value) <= maxWidth)
        {
            return value;
        }

        var end = value.Length;
        while (end > 0 && paint.MeasureText($"{value[..end]}…") > maxWidth)
        {
            end--;
        }

        return end == 0 ? "…" : $"{value[..end]}…";
    }

    private static string AppendEllipsis(string value, SKPaint paint, float maxWidth)
    {
        while (value.Length > 0 && paint.MeasureText($"{value}…") > maxWidth)
        {
            value = value[..^1];
        }

        return $"{value}…";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (safe.Length > 48)
        {
            safe = safe[..48].Trim();
        }

        return string.IsNullOrWhiteSpace(safe) ? "corob" : safe;
    }
}
