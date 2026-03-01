using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Infrastructure;

public sealed class ConfigurableFileLogger : IAppLogger
{
    private readonly object _sync = new();
    private readonly string _filePath;
    private bool _fileLoggingEnabled;

    public ConfigurableFileLogger(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public void SetFileLogging(bool enabled)
    {
        _fileLoggingEnabled = enabled;
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var value = exception is null ? message : $"{message} :: {exception}";
        Write("ERROR", value);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
        Console.WriteLine(line);

        if (!_fileLoggingEnabled)
        {
            return;
        }

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
    }
}
