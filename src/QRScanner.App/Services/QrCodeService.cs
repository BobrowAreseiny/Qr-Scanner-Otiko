using QRCoder;
using SkiaSharp;
using System.Runtime.Versioning;
using Android.Content;
using Android.Provider;

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

    public async Task<QrFileSaveResult> GetOrCreateAsync(
        int corobId,
        string corobName,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"{SanitizeFileName(corobName)}.png";

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            try
            {
                var existing = FindInDownloads(fileName);
                if (existing is not null)
                {
                    return existing;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось проверить QR в Downloads: {ex}");
            }
        }

        var privatePath = Path.Combine(
            FileSystem.AppDataDirectory,
            "Otiko",
            "QR-коды",
            fileName);

        if (File.Exists(privatePath))
        {
            return new QrFileSaveResult(
                fileName,
                privatePath,
                privatePath,
                IsPublic: false,
                Created: false);
        }

        return await SaveToDownloadsAsync(corobId, corobName, cancellationToken);
    }

    [SupportedOSPlatform("android29.0")]
    private static QrFileSaveResult? FindInDownloads(string fileName)
    {
        var context = Android.App.Application.Context;
        var resolver = context.ContentResolver;
        if (resolver is null)
        {
            return null;
        }

        var collection = MediaStore.Downloads.ExternalContentUri;
        var projection = new[]
        {
            "_id",
            MediaStore.IMediaColumns.RelativePath
        };
        var selection = $"{MediaStore.IMediaColumns.DisplayName} = ?";

        using var cursor = resolver.Query(
            collection,
            projection,
            selection,
            new[] { fileName },
            sortOrder: null);

        if (cursor is null)
        {
            return null;
        }

        var idColumn = cursor.GetColumnIndexOrThrow("_id");
        var pathColumn = cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.RelativePath);

        while (cursor.MoveToNext())
        {
            var relative = cursor.GetString(pathColumn) ?? string.Empty;
            var normalized = relative.Replace('\\', '/');
            if (!normalized.StartsWith(
                    "Download/Otiko/QR-коды/",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = cursor.GetLong(idColumn);
            var uri = ContentUris.WithAppendedId(collection, id);

            return new QrFileSaveResult(
                fileName,
                $"Download/Otiko/QR-коды/{fileName}",
                uri.ToString() ?? string.Empty,
                IsPublic: true,
                Created: false);
        }

        return null;
    }

    public async Task<QrFileSaveResult> SaveToDownloadsAsync(
        int corobId,
        string corobName,
        CancellationToken cancellationToken = default)
    {
        var cachePath = await GenerateAsync(corobId, corobName);
        var fileName = $"{SanitizeFileName(corobName)}.png";

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            try
            {
                return await SaveToDownloadsCoreAsync(
                    cachePath,
                    fileName,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось сохранить QR в Downloads: {ex}");
            }
        }

        var directory = Path.Combine(
            FileSystem.AppDataDirectory,
            "Otiko",
            "QR-коды");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        File.Copy(cachePath, path, overwrite: true);

        return new QrFileSaveResult(
            fileName,
            path,
            path,
            IsPublic: false);
    }

    public Task DeleteAsync(
        int corobId,
        string corobName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(corobName))
        {
            return Task.CompletedTask;
        }

        var fileName = $"{SanitizeFileName(corobName)}.png";

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            try
            {
                var existing = FindInDownloads(fileName);
                if (existing is not null &&
                    !string.IsNullOrWhiteSpace(existing.Location))
                {
                    var uri = Android.Net.Uri.Parse(existing.Location);
                    if (uri is not null)
                    {
                        Android.App.Application.Context.ContentResolver?
                            .Delete(uri, null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось удалить QR из Downloads: {ex}");
            }
        }

        var privatePath = Path.Combine(
            FileSystem.AppDataDirectory,
            "Otiko",
            "QR-коды",
            fileName);
        try
        {
            if (File.Exists(privatePath))
            {
                File.Delete(privatePath);
            }
        }
        catch
        {
            // Старый файл не критичен.
        }

        var cacheDirectory = Path.Combine(FileSystem.CacheDirectory, "qr");
        if (Directory.Exists(cacheDirectory))
        {
            foreach (var oldFile in Directory.EnumerateFiles(
                cacheDirectory,
                $"{corobId}-*.png"))
            {
                try
                {
                    File.Delete(oldFile);
                }
                catch
                {
                    // Старый файл не критичен.
                }
            }
        }

        return Task.CompletedTask;
    }

    [SupportedOSPlatform("android29.0")]
    private static async Task<QrFileSaveResult> SaveToDownloadsCoreAsync(
        string cachePath,
        string fileName,
        CancellationToken cancellationToken)
    {
        var context = Android.App.Application.Context;
        var resolver = context.ContentResolver
            ?? throw new InvalidOperationException("ContentResolver недоступен.");

        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
        values.Put(MediaStore.IMediaColumns.MimeType, "image/png");
        values.Put(
            MediaStore.IMediaColumns.RelativePath,
            $"{Android.OS.Environment.DirectoryDownloads}/Otiko/QR-коды");

        var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values)
            ?? throw new InvalidOperationException(
                "Не удалось создать PNG в Download/Otiko/QR-коды.");

        await using (var output = resolver.OpenOutputStream(uri)
            ?? throw new InvalidOperationException(
                "Не удалось открыть PNG в Download/Otiko/QR-коды."))
        {
            await using var input = File.OpenRead(cachePath);
            await input.CopyToAsync(output, cancellationToken);
        }

        return new QrFileSaveResult(
            fileName,
            $"Download/Otiko/QR-коды/{fileName}",
            uri.ToString() ?? string.Empty,
            IsPublic: true);
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
