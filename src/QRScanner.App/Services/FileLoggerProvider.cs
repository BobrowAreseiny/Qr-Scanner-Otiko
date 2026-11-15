using Microsoft.Extensions.Logging;
using QRScanner.Core.Logging;

namespace QRScanner.App.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly LogFileWriter _writer;
    private bool _disposed;

    public FileLoggerProvider(Func<string> directoryProvider)
    {
        _writer = new LogFileWriter(directoryProvider);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _writer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writer.Dispose();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly LogFileWriter _writer;

        public FileLogger(string category, LogFileWriter writer)
        {
            _category = category;
            _writer = writer;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None)
            {
                return false;
            }

            if (_category.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                _category.StartsWith("System.", StringComparison.Ordinal))
            {
                return logLevel >= LogLevel.Warning;
            }

            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _writer.Write(
                logLevel,
                _category,
                formatter(state, exception),
                exception);
        }
    }
}

internal sealed class LogFileWriter : IDisposable
{
    private readonly Func<string> _directoryProvider;
    private readonly object _gate = new();

    private StreamWriter? _writer;
    private DateTime _date;
    private bool _disposed;

    public LogFileWriter(Func<string> directoryProvider)
    {
        _directoryProvider = directoryProvider;
    }

    public void Write(LogLevel level, string category, string message, Exception? exception)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var now = DateTime.Now;
                EnsureWriter(now);

                _writer!.WriteLine(
                    $"{now:yyyy-MM-dd HH:mm:ss.fff} [{ToLabel(level)}] {category}: {LogSanitizer.Sanitize(message)}");

                if (exception is not null)
                {
                    _writer.WriteLine(LogSanitizer.Sanitize(exception.ToString()));
                }
            }
            catch
            {
                // Ошибка записи лога не должна ронять приложение.
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void EnsureWriter(DateTime now)
    {
        if (_writer is not null && _date == now.Date)
        {
            return;
        }

        _writer?.Dispose();

        var directory = _directoryProvider();
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, LogFileNames.ForDate(now));
        var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);

        _writer = new StreamWriter(stream)
        {
            AutoFlush = true
        };

        _date = now.Date;
    }

    private static string ToLabel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "FATAL",
            _ => "LOG"
        };
    }
}
