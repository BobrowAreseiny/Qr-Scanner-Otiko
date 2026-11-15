using System.Text.Json;
using QRScanner.Core;

namespace QRScanner.App.Services;

public sealed record ImportedCredentials(
    string BaseUrl,
    string Login,
    string Password,
    string SourcePath);

public interface ICredentialsImporter
{
    Task<ImportedCredentials?> TryImportAsync();

    Task<int> DeleteLegacyFilesAsync();
}

public sealed class LegacyCredentialsImporter : ICredentialsImporter
{
    private const string FileName = "credentials.txt";

    public Task<ImportedCredentials?> TryImportAsync()
    {
        foreach (var path in GetCandidatePaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var text = File.ReadAllText(path);
                var parsed = Parse(text);
                if (parsed is null)
                {
                    continue;
                }

                return Task.FromResult<ImportedCredentials?>(new ImportedCredentials(
                    AppConstants.ApiBaseUrl,
                    parsed.Value.Login,
                    parsed.Value.Password,
                    path));
            }
            catch
            {
                // Файл недоступен или имеет другой формат.
            }
        }

        return Task.FromResult<ImportedCredentials?>(null);
    }

    public Task<int> DeleteLegacyFilesAsync()
    {
        var deleted = 0;

        foreach (var path in GetCandidatePaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                File.Delete(path);
                deleted++;
            }
            catch
            {
                // Файл недоступен для удаления.
            }
        }

        return Task.FromResult(deleted);
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        yield return Path.Combine(FileSystem.AppDataDirectory, FileName);

        var external = Android.App.Application.Context
            .GetExternalFilesDir(null)?
            .AbsolutePath;

        if (!string.IsNullOrWhiteSpace(external))
        {
            yield return Path.Combine(external, FileName);
        }

        yield return "/sdcard/Android/data/com.companyname.qrscanner/files/credentials.txt";
    }

    private static (string Login, string Password)? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();

        if (trimmed.StartsWith('{'))
        {
            return ParseJson(trimmed);
        }

        var lines = trimmed
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        if (lines.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = line.IndexOf(':');
            }

            if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        var login = FindValue(values, "username", "login", "email", "user");
        var password = FindValue(values, "password", "pass", "pwd");

        if (!string.IsNullOrWhiteSpace(login) && !string.IsNullOrEmpty(password))
        {
            return (login, password);
        }

        if (lines.Count >= 2)
        {
            return (lines[0], lines[1]);
        }

        var parts = lines[0].Split(';', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            return (parts[0], parts[1]);
        }

        return null;
    }

    private static (string Login, string Password)? ParseJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            var login = FindJsonValue(root, "username", "login", "email", "user");
            var password = FindJsonValue(root, "password", "pass", "pwd");

            if (!string.IsNullOrWhiteSpace(login) && !string.IsNullOrEmpty(password))
            {
                return (login, password);
            }
        }
        catch (JsonException)
        {
            // Не JSON.
        }

        return null;
    }

    private static string? FindValue(
        IReadOnlyDictionary<string, string> values,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? FindJsonValue(JsonElement root, params string[] keys)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (keys.Any(key => string.Equals(key, property.Name, StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }
}
