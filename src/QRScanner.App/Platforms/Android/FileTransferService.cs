using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace QRScanner.App.Services;

public sealed class FileTransferService : IFileTransferService
{
    private const int SaveFileRequestCode = 0x5346;
    private const int PickFileRequestCode = 0x5347;

    private PendingSave? _pendingSave;
    private PendingPick? _pendingPick;

    public Task<bool> SaveAsync(string sourcePath, string suggestedName, string mimeType)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("Нет активного окна приложения.");

        _pendingSave?.Completion.TrySetResult(false);
        _pendingSave = null;

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingSave = new PendingSave(sourcePath, completion);

        using var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mimeType);
        intent.PutExtra(Intent.ExtraTitle, suggestedName);

        activity.StartActivityForResult(intent, SaveFileRequestCode);
        return completion.Task;
    }

    public Task<string?> PickAsync(string mimeType)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("Нет активного окна приложения.");

        _pendingPick?.Completion.TrySetResult(null);
        _pendingPick = null;

        var completion = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingPick = new PendingPick(completion);

        using var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mimeType);

        activity.StartActivityForResult(intent, PickFileRequestCode);
        return completion.Task;
    }

    public void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode == SaveFileRequestCode)
        {
            HandleSaveResult(resultCode, data);
            return;
        }

        if (requestCode == PickFileRequestCode)
        {
            HandlePickResult(resultCode, data);
        }
    }

    private void HandleSaveResult(Result resultCode, Intent? data)
    {
        var pending = _pendingSave;
        _pendingSave = null;

        if (pending is null)
        {
            return;
        }

        if (resultCode != Result.Ok || data?.Data is null)
        {
            pending.Completion.TrySetResult(false);
            return;
        }

        _ = WriteAsync(pending, data.Data);
    }

    private void HandlePickResult(Result resultCode, Intent? data)
    {
        var pending = _pendingPick;
        _pendingPick = null;

        if (pending is null)
        {
            return;
        }

        if (resultCode != Result.Ok || data?.Data is null)
        {
            pending.Completion.TrySetResult(null);
            return;
        }

        _ = CopyPickedFileAsync(pending, data.Data);
    }

    private static async Task WriteAsync(PendingSave pending, Android.Net.Uri uri)
    {
        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            var resolver = context.ContentResolver
                ?? throw new IOException("Не удалось получить доступ к файлам.");

            await using var source = File.OpenRead(pending.SourcePath);
            await using var target = resolver.OpenOutputStream(uri)
                ?? throw new IOException("Не удалось открыть выбранный файл.");

            await source.CopyToAsync(target);
            await target.FlushAsync();
            pending.Completion.TrySetResult(true);
        }
        catch (Exception ex)
        {
            pending.Completion.TrySetException(ex);
        }
    }

    private static async Task CopyPickedFileAsync(PendingPick pending, Android.Net.Uri uri)
    {
        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            var resolver = context.ContentResolver
                ?? throw new IOException("Не удалось получить доступ к файлам.");

            var directory = Path.Combine(FileSystem.CacheDirectory, "imports");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(
                directory,
                $"import-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db");

            await using var source = resolver.OpenInputStream(uri)
                ?? throw new IOException("Не удалось прочитать выбранный файл.");
            await using var target = File.Create(path);

            await source.CopyToAsync(target);
            await target.FlushAsync();
            pending.Completion.TrySetResult(path);
        }
        catch (Exception ex)
        {
            pending.Completion.TrySetException(ex);
        }
    }

    private sealed record PendingSave(
        string SourcePath,
        TaskCompletionSource<bool> Completion);

    private sealed record PendingPick(
        TaskCompletionSource<string?> Completion);
}
