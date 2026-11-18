using System.Linq;
using QRScanner.App.Data;
using QRScanner.App.Data.Entities;

namespace QRScanner.App.Services;

public static class TransferKinds
{
    public const string Order = "order";
    public const string Qr = "qr";
}

public interface ITransferQueueService
{
    Task EnqueueAsync(
        string sourceLocation,
        string fileName,
        string kind,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync();

    Task<TransferSendResult> SendPendingAsync(CancellationToken cancellationToken = default);
}

public sealed record TransferSendResult(int Sent, int Failed, string Message);

public sealed class TransferQueueService : ITransferQueueService
{
    private readonly IDatabaseService _database;
    private readonly ISmbSettingsService _smbSettings;
    private readonly ISmbFileTransferService _smbTransfer;

    public TransferQueueService(
        IDatabaseService database,
        ISmbSettingsService smbSettings,
        ISmbFileTransferService smbTransfer)
    {
        _database = database;
        _smbSettings = smbSettings;
        _smbTransfer = smbTransfer;
    }

    public async Task EnqueueAsync(
        string sourceLocation,
        string fileName,
        string kind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceLocation))
        {
            return;
        }

        var safeName = SanitizeFileName(fileName);
        var pending = await _database.GetPendingTransfersAsync();
        if (pending.Any(item =>
                string.Equals(item.FileName, safeName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var queueDirectory = Path.Combine(FileSystem.AppDataDirectory, "TransferQueue");
        Directory.CreateDirectory(queueDirectory);

        var localName = Guid.NewGuid().ToString("N") + "_" + safeName;
        var localPath = Path.Combine(queueDirectory, localName);

        await using (var destination = File.Create(localPath))
        {
            if (sourceLocation.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                var resolver = Android.App.Application.Context.ContentResolver
                    ?? throw new InvalidOperationException("ContentResolver недоступен.");
                var uri = Android.Net.Uri.Parse(sourceLocation)
                    ?? throw new InvalidOperationException("Некорректный URI файла.");
                using var input = resolver.OpenInputStream(uri)
                    ?? throw new InvalidOperationException("Не удалось открыть файл.");
                await input.CopyToAsync(destination, cancellationToken);
            }
            else
            {
                await using var input = File.OpenRead(sourceLocation);
                await input.CopyToAsync(destination, cancellationToken);
            }
        }

        await _database.SaveTransferAsync(new TransferQueueEntity
        {
            FileName = safeName,
            LocalPath = localPath,
            Kind = kind,
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public Task<int> CountPendingAsync()
    {
        return _database.CountPendingTransfersAsync();
    }

    public async Task<TransferSendResult> SendPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _smbSettings.LoadAsync();
        if (!settings.IsConfigured)
        {
            return new TransferSendResult(
                0,
                0,
                "Общая папка не настроена. Откройте Настройки → Компьютер (SMB).");
        }

        var pending = await _database.GetPendingTransfersAsync();
        if (pending.Count == 0)
        {
            return new TransferSendResult(0, 0, "Нет файлов для отправки.");
        }

        var sent = 0;
        var failed = 0;
        string? firstError = null;

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(item.LocalPath))
            {
                failed++;
                firstError ??= $"Файл очереди не найден: {item.FileName}.";
                await _database.DeleteTransferAsync(item.Id);
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(item.LocalPath);
                var result = await _smbTransfer.UploadAsync(
                    settings,
                    item.FileName,
                    stream,
                    cancellationToken);

                if (result.Success)
                {
                    sent++;
                    try
                    {
                        File.Delete(item.LocalPath);
                    }
                    catch
                    {
                        // The original file remains in Download/Otiko.
                    }

                    await _database.DeleteTransferAsync(item.Id);
                }
                else
                {
                    failed++;
                    item.Attempts++;
                    item.LastError = result.Message;
                    await _database.SaveTransferAsync(item);
                    firstError ??= result.Message;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                item.Attempts++;
                item.LastError = ex.Message;
                await _database.SaveTransferAsync(item);
                firstError ??= ex.Message;
            }
        }

        var message = failed == 0
            ? $"Отправлено файлов: {sent}."
            : $"Отправлено: {sent}. Ошибок: {failed}. {firstError}";

        return new TransferSendResult(sent, failed, message);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        var separator = name.LastIndexOf('/');
        if (separator >= 0)
        {
            name = name[(separator + 1)..];
        }

        var invalid = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var value in name)
        {
            builder.Append(
                invalid.Contains(value) || char.IsControl(value)
                    ? '_'
                    : value);
        }

        var safe = builder.ToString().Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "file";
        }

        var extension = Path.GetExtension(safe);
        var baseName = Path.GetFileNameWithoutExtension(safe);
        if (baseName.Length > 100)
        {
            baseName = baseName[..100].Trim();
        }

        return baseName + extension;
    }
}
