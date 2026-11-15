using System.IO.Compression;
using System.Text;
using QRScanner.Core.Logging;

namespace QRScanner.App.Services;

public sealed record LogFileInfo(string Path, DateTime Date, long SizeBytes);

public interface ILogService
{
    string DirectoryPath { get; }

    IReadOnlyList<LogFileInfo> GetFiles();

    Task<string?> CreateArchiveAsync();

    Task<string> ReadLatestAsync(int maxChars = 20000);
}

public sealed class LogService : ILogService
{
    public string DirectoryPath => Path.Combine(FileSystem.AppDataDirectory, "Logs");

    public IReadOnlyList<LogFileInfo> GetFiles()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return [];
        }

        var files = new List<LogFileInfo>();

        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.log"))
        {
            var fileName = Path.GetFileName(path);
            if (!LogFileNames.TryParseDate(fileName, out var date))
            {
                continue;
            }

            files.Add(new LogFileInfo(path, date, new FileInfo(path).Length));
        }

        files.Sort((left, right) => right.Date.CompareTo(left.Date));
        return files;
    }

    public Task<string?> CreateArchiveAsync()
    {
        var files = GetFiles();
        if (files.Count == 0)
        {
            return Task.FromResult<string?>(null);
        }

        var path = Path.Combine(
            FileSystem.CacheDirectory,
            $"logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        ZipFile.CreateFromDirectory(
            DirectoryPath,
            path,
            CompressionLevel.Fastest,
            includeBaseDirectory: false);

        return Task.FromResult<string?>(path);
    }

    public async Task<string> ReadLatestAsync(int maxChars = 20000)
    {
        var latest = GetFiles().FirstOrDefault();
        if (latest is null)
        {
            return "Логи отсутствуют.";
        }

        await using var stream = new FileStream(
            latest.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();

        if (text.Length <= maxChars)
        {
            return text;
        }

        return "…" + text[^maxChars..];
    }
}
