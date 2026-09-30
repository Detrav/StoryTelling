using System.Text;
using Microsoft.Extensions.Logging;

namespace StoryTelling.Infrastructure.Logging;

internal sealed class FileLogger : ILogger
{
    private readonly string _category;
    private readonly FileLoggerProvider _provider;

    public FileLogger(string category, FileLoggerProvider provider)
    {
        _category = category;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

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

        var builder = new StringBuilder();
        builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.Append(" [").Append(logLevel).Append("] ");
        builder.Append(_category).Append(": ");
        builder.Append(formatter(state, exception));

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        _provider.Write(builder.ToString());
    }
}
