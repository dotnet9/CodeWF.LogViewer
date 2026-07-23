using CodeWF.Log.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

internal sealed class ConsoleLogSink : ILogSink
{
    private readonly ConsoleLogOptions _options;

    public ConsoleLogSink(ConsoleLogOptions options)
    {
        _options = options;
    }

    public ValueTask WriteAsync(CodeWFLogEvent logEvent, CancellationToken cancellationToken)
    {
        if (_options.UserLogOnly && logEvent.UserLog is null) return ValueTask.CompletedTask;

        var previousColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = GetColor(logEvent.Level);
            Console.Write(Format(logEvent));
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }

        return ValueTask.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private string Format(CodeWFLogEvent logEvent)
    {
        if (!string.IsNullOrWhiteSpace(_options.OutputTemplate))
        {
            var text = LogOutputTemplateFormatter.Format(
                logEvent,
                _options.OutputTemplate,
                _options.TimestampFormat);
            return text.EndsWith(Environment.NewLine, StringComparison.Ordinal)
                ? text
                : text + Environment.NewLine;
        }

        var message = _options.UserLogOnly
            ? logEvent.UserLog?.Message ?? logEvent.Message
            : logEvent.Message;

        return $"{logEvent.Timestamp.ToString(_options.TimestampFormat)} [{logEvent.Level.Description()}] {message}{Environment.NewLine}";
    }

    private static ConsoleColor GetColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace or LogLevel.Debug => ConsoleColor.Cyan,
            LogLevel.Information => ConsoleColor.Green,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
    }
}
