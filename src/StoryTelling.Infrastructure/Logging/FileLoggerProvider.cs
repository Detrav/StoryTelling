using Microsoft.Extensions.Logging;

namespace StoryTelling.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    public FileLoggerProvider(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void Write(string line)
    {
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }
}
