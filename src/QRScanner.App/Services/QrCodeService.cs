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

        var safeName = SanitizeFileName(corobName);
        var path = Path.Combine(directory, $"{corobId}-{safeName}.png");
        await File.WriteAllBytesAsync(path, pngBytes);
        return path;
    }

    private static byte[] AddText(byte[] qrBytes, string text)
    {
        using var original = SKBitmap.Decode(qrBytes)
            ?? throw new InvalidOperationException("Не удалось декодировать QR-код.");

        var textSize = Math.Max(original.Width * 0.08f, 28f);
        var padding = (int)(textSize / 2);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = textSize,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);

        var width = original.Width;
        var height = original.Height + (int)bounds.Height + padding * 2;
        var info = new SKImageInfo(width, height);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(original, 0, 0);
        canvas.DrawText(
            text,
            width / 2f,
            original.Height + padding - bounds.Top,
            paint);

        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "corob" : safe;
    }
}
