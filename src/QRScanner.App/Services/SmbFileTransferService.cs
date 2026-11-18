using System.Linq;
using System.Net;
using SMBLibrary;
using SMBLibrary.Client;

namespace QRScanner.App.Services;

public interface ISmbFileTransferService
{
    Task<SmbOperationResult> TestConnectionAsync(
        SmbConnectionSettings settings,
        CancellationToken cancellationToken = default);

    Task<SmbOperationResult> UploadAsync(
        SmbConnectionSettings settings,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default,
        bool skipIfExists = false);

    Task<SmbDownloadResult> DownloadAsync(
        SmbConnectionSettings settings,
        string remoteFolder,
        string remoteFileName,
        Stream destination,
        CancellationToken cancellationToken = default);
}

public sealed record SmbOperationResult(
    bool Success,
    string Message,
    bool Skipped = false);

public sealed record SmbDownloadResult(bool Success, string Message, long Size);

public sealed class SmbFileTransferService : ISmbFileTransferService
{
    private const string OrdersFolder = "Заказы";
    private const string QrFolder = "QR-коды";
    private const int MaxFileSize = 50 * 1024 * 1024;
    private const long MaxDownloadSize = 200 * 1024 * 1024;
    private const int ResponseTimeoutMilliseconds = 15000;

    public Task<SmbOperationResult> TestConnectionAsync(
        SmbConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => TestConnectionCore(settings.Normalize(), cancellationToken),
            cancellationToken);
    }

    public Task<SmbOperationResult> UploadAsync(
        SmbConnectionSettings settings,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default,
        bool skipIfExists = false)
    {
        return Task.Run(
            () => UploadCore(
                settings.Normalize(),
                fileName,
                content,
                skipIfExists,
                cancellationToken),
            cancellationToken);
    }

    public Task<SmbDownloadResult> DownloadAsync(
        SmbConnectionSettings settings,
        string remoteFolder,
        string remoteFileName,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => DownloadCore(
                settings.Normalize(),
                remoteFolder,
                remoteFileName,
                destination,
                cancellationToken),
            cancellationToken);
    }

    private static SmbOperationResult TestConnectionCore(
        SmbConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var validation = Validate(settings);
        if (validation is not null)
        {
            return new SmbOperationResult(false, validation);
        }

        var testFileName = "_qrscanner_test_" + Guid.NewGuid().ToString("N") + ".txt";
        var testPath = OrdersFolder + "\\" + testFileName;
        SMB2Client? client = null;
        var connected = false;
        var loggedIn = false;
        ISMBFileStore? fileStore = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            client = new SMB2Client(ResponseTimeoutMilliseconds);
            if (!IPAddress.TryParse(settings.Server, out var address))
            {
                return new SmbOperationResult(
                    false,
                    "Укажите IP-адрес компьютера, например 192.168.0.15.");
            }

            if (!client.Connect(address, SMBTransportType.DirectTCPTransport))
            {
                return new SmbOperationResult(
                    false,
                    "Компьютер не отвечает по SMB. Проверьте сеть и порт 445.");
            }

            connected = true;

            var status = Login(client, settings);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new SmbOperationResult(
                    false,
                    "Не удалось войти на компьютер: " + Describe(status));
            }

            loggedIn = true;

            fileStore = client.TreeConnect(settings.Share, out status);
            if (status != NTStatus.STATUS_SUCCESS || fileStore is null)
            {
                return new SmbOperationResult(
                    false,
                    $"Не удалось открыть папку «{settings.Share}»: " + Describe(status));
            }

            var data = System.Text.Encoding.UTF8.GetBytes("QRScanner test");
            status = fileStore.CreateFile(
                out var handle,
                out _,
                testPath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OVERWRITE_IF,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new SmbOperationResult(
                    false,
                    "Не удалось создать тестовый файл: " + Describe(status));
            }

            status = fileStore.WriteFile(out _, handle, 0, data);
            fileStore.CloseFile(handle);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new SmbOperationResult(
                    false,
                    "Не удалось записать тестовый файл: " + Describe(status));
            }

            TryDelete(fileStore, testPath);
            return new SmbOperationResult(
                true,
                $"Подключение работает. Папка «{settings.Share}» доступна для записи.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SmbOperationResult(false, $"Ошибка SMB: {ex.Message}");
        }
        finally
        {
            fileStore?.Disconnect();

            if (loggedIn)
            {
                try
                {
                    client?.Logoff();
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }

            if (connected)
            {
                try
                {
                    client?.Disconnect();
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }
        }
    }

    private static SmbOperationResult UploadCore(
        SmbConnectionSettings settings,
        string fileName,
        Stream content,
        bool skipIfExists,
        CancellationToken cancellationToken)
    {
        var validation = Validate(settings);
        if (validation is not null)
        {
            return new SmbOperationResult(false, validation);
        }

        var extension = Path.GetExtension(fileName);
        string folder;
        if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
        {
            folder = OrdersFolder;
        }
        else if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
        {
            folder = QrFolder;
        }
        else
        {
            return new SmbOperationResult(
                false,
                "Передаются только файлы .txt и .png.");
        }

        var safeName = SanitizeFileName(fileName);
        var remotePath = folder + "\\" + safeName;
        SMB2Client? client = null;
        var connected = false;
        var loggedIn = false;
        ISMBFileStore? fileStore = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var memory = new MemoryStream();
            content.CopyTo(memory);
            if (memory.Length > MaxFileSize)
            {
                return new SmbOperationResult(false, "Файл слишком большой.");
            }

            var data = memory.ToArray();

            client = new SMB2Client(ResponseTimeoutMilliseconds);
            if (!IPAddress.TryParse(settings.Server, out var address))
            {
                return new SmbOperationResult(
                    false,
                    "Укажите IP-адрес компьютера, например 192.168.0.15.");
            }

            if (!client.Connect(address, SMBTransportType.DirectTCPTransport))
            {
                return new SmbOperationResult(
                    false,
                    "Компьютер не отвечает по SMB. Проверьте сеть и порт 445.");
            }

            connected = true;

            var status = Login(client, settings);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new SmbOperationResult(
                    false,
                    "Не удалось войти на компьютер: " + Describe(status));
            }

            loggedIn = true;

            fileStore = client.TreeConnect(settings.Share, out status);
            if (status != NTStatus.STATUS_SUCCESS || fileStore is null)
            {
                return new SmbOperationResult(
                    false,
                    $"Не удалось открыть папку «{settings.Share}»: " + Describe(status));
            }

            if (skipIfExists)
            {
                var existingStatus = fileStore.CreateFile(
                    out var existingHandle,
                    out _,
                    remotePath,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null);

                if (existingStatus == NTStatus.STATUS_SUCCESS)
                {
                    fileStore.CloseFile(existingHandle);
                    return new SmbOperationResult(
                        true,
                        $"Файл «{safeName}» уже есть на компьютере.",
                        Skipped: true);
                }

                if (existingStatus != NTStatus.STATUS_OBJECT_NAME_NOT_FOUND &&
                    existingStatus != NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
                {
                    return new SmbOperationResult(
                        false,
                        $"Не удалось проверить файл «{safeName}»: " +
                        Describe(existingStatus));
                }
            }

            status = fileStore.CreateFile(
                out var handle,
                out _,
                remotePath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OVERWRITE_IF,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new SmbOperationResult(
                    false,
                    $"Не удалось открыть файл «{remotePath}» на компьютере: " +
                    Describe(status));
            }

            var offset = 0L;
            var chunkSize = (int)client.MaxWriteSize;
            if (chunkSize <= 0)
            {
                chunkSize = 64 * 1024;
            }

            while (offset < data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var count = Math.Min(chunkSize, data.Length - (int)offset);
                var chunk = new byte[count];
                Buffer.BlockCopy(data, (int)offset, chunk, 0, count);

                status = fileStore.WriteFile(out _, handle, offset, chunk);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    fileStore.CloseFile(handle);
                    return new SmbOperationResult(
                        false,
                        $"Не удалось записать файл «{safeName}»: " +
                        Describe(status));
                }

                offset += count;
            }

            fileStore.CloseFile(handle);
            return new SmbOperationResult(
                true,
                $"Файл «{safeName}» отправлен в «{folder}».");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SmbOperationResult(false, $"Ошибка SMB: {ex.Message}");
        }
        finally
        {
            fileStore?.Disconnect();

            if (loggedIn)
            {
                try
                {
                    client?.Logoff();
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }

            if (connected)
            {
                try
                {
                    client?.Disconnect();
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }
        }
    }

    private static SmbDownloadResult DownloadCore(
        SmbConnectionSettings settings,
        string remoteFolder,
        string remoteFileName,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var validation = Validate(settings);
        if (validation is not null)
        {
            return new SmbDownloadResult(false, validation, 0);
        }

        var folder = SanitizeFolder(remoteFolder);
        var safeName = SanitizeFileName(remoteFileName);
        var remotePath = string.IsNullOrEmpty(folder)
            ? safeName
            : folder + "\\" + safeName;

        SMB2Client? client = null;
        var connected = false;
        var loggedIn = false;
        ISMBFileStore? fileStore = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            client = new SMB2Client(ResponseTimeoutMilliseconds);
            if (!IPAddress.TryParse(settings.Server, out var address))
            {
                return new SmbDownloadResult(
                    false,
                    "Укажите IP-адрес компьютера, например 192.168.0.15.",
                    0);
            }

            if (!client.Connect(address, SMBTransportType.DirectTCPTransport))
            {
                return new SmbDownloadResult(
                    false,
                    "Компьютер не отвечает по SMB. Проверьте сеть и порт 445.",
                    0);
            }

            connected = true;

            var status = Login(client, settings);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new SmbDownloadResult(
                    false,
                    "Не удалось войти на компьютер: " + Describe(status),
                    0);
            }

            loggedIn = true;

            fileStore = client.TreeConnect(settings.Share, out status);
            if (status != NTStatus.STATUS_SUCCESS || fileStore is null)
            {
                return new SmbDownloadResult(
                    false,
                    $"Не удалось открыть папку «{settings.Share}»: " + Describe(status),
                    0);
            }

            status = fileStore.CreateFile(
                out var handle,
                out _,
                remotePath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                var message = status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND
                    ? $"На компьютере нет файла «{remotePath}»."
                    : $"Не удалось открыть файл «{remotePath}»: " + Describe(status);
                return new SmbDownloadResult(false, message, 0);
            }

            status = fileStore.GetFileInformation(
                out var fileInfo,
                handle,
                FileInformationClass.FileStandardInformation);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                fileStore.CloseFile(handle);
                return new SmbDownloadResult(
                    false,
                    "Не удалось получить размер файла: " + Describe(status),
                    0);
            }

            var size = ((FileStandardInformation)fileInfo).EndOfFile;
            if (size > MaxDownloadSize)
            {
                fileStore.CloseFile(handle);
                return new SmbDownloadResult(
                    false,
                    "Файл слишком большой для обновления.",
                    0);
            }

            var offset = 0L;
            var chunkSize = (int)client.MaxReadSize;
            if (chunkSize <= 0)
            {
                chunkSize = 64 * 1024;
            }

            while (offset < size)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var count = (int)Math.Min(chunkSize, size - offset);
                status = fileStore.ReadFile(out var chunk, handle, offset, count);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    fileStore.CloseFile(handle);
                    return new SmbDownloadResult(
                        false,
                        $"Не удалось прочитать файл «{safeName}»: " +
                        Describe(status),
                        0);
                }

                if (chunk.Length == 0)
                {
                    break;
                }

                destination.Write(chunk, 0, chunk.Length);
                offset += chunk.Length;
            }

            fileStore.CloseFile(handle);
            destination.Flush();

            return new SmbDownloadResult(
                true,
                $"Файл «{safeName}» получен с компьютера.",
                offset);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SmbDownloadResult(false, $"Ошибка SMB: {ex.Message}", 0);
        }
        finally
        {
            fileStore?.Disconnect();

            if (loggedIn)
            {
                try
                {
                    client?.Logoff();
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }

            if (connected)
            {
                try
                {
                    client?.Disconnect();
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }
        }
    }

    private static NTStatus Login(SMB2Client client, SmbConnectionSettings settings)
    {
        var status = client.Login(settings.Domain, settings.Login, settings.Password);
        if (status == NTStatus.STATUS_LOGON_FAILURE &&
            !string.IsNullOrWhiteSpace(settings.Domain))
        {
            status = client.Login(string.Empty, settings.Login, settings.Password);
        }

        return status;
    }

    private static void TryDelete(ISMBFileStore fileStore, string path)
    {
        try
        {
            var status = fileStore.CreateFile(
                out var handle,
                out _,
                path,
                AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                return;
            }

            var info = new FileDispositionInformation { DeletePending = true };
            fileStore.SetFileInformation(handle, info);
            fileStore.CloseFile(handle);
        }
        catch
        {
            // A leftover test file is harmless.
        }
    }

    private static string? Validate(SmbConnectionSettings settings)
    {
        if (!settings.IsConfigured)
        {
            return "Заполните сервер, папку, пользователя и пароль.";
        }

        if (!IPAddress.TryParse(settings.Server, out _))
        {
            return "Укажите IP-адрес компьютера, например 192.168.0.15.";
        }

        return null;
    }

    private static string Describe(NTStatus status)
    {
        return status switch
        {
            NTStatus.STATUS_LOGON_FAILURE => "Неверный пользователь или пароль.",
            NTStatus.STATUS_ACCESS_DENIED => "Нет прав на общую папку.",
            NTStatus.STATUS_BAD_NETWORK_NAME => "Общая папка не найдена.",
            NTStatus.STATUS_OBJECT_NAME_NOT_FOUND => "Папка не найдена на компьютере.",
            _ => $"Ошибка SMB: {status}."
        };
    }

    private static string SanitizeFolder(string folder)
    {
        var value = (folder ?? string.Empty)
            .Replace('/', '\\')
            .Trim('\\', ' ');

        if (value.Contains("..", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return value;
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
