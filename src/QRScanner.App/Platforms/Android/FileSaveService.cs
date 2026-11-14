using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace QRScanner.App.Services;

public sealed class FileSaveService : IFileSaveService
{
    private const int SaveFileRequestCode = 0x5346;
    private PendingSave? _pending;

    public Task<bool> SaveAsync(string sourcePath, string suggestedName, string mimeType)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("Нет активного окна приложения.");

        _pending?.Completion.TrySetResult(false);
        _pending = null;

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = new PendingSave(sourcePath, completion);

        using var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mimeType);
        intent.PutExtra(Intent.ExtraTitle, suggestedName);

        activity.StartActivityForResult(intent, SaveFileRequestCode);
        return completion.Task;
    }

    public void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != SaveFileRequestCode)
        {
            return;
        }

        var pending = _pending;
        _pending = null;

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

    private sealed record PendingSave(
        string SourcePath,
        TaskCompletionSource<bool> Completion);
}
