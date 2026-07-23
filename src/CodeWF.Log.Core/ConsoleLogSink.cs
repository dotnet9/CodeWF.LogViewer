using CodeWF.Log.Core.Extensions;

namespace CodeWF.Log.Core;

internal sealed class ConsoleLogSink : ILogSink
{
    private readonly string _timestampFormat;

    public ConsoleLogSink(string timestampFormat)
    {
        _timestampFormat = timestampFormat;
    }

    public ValueTask WriteAsync(LogEvent logEvent, CancellationToken cancellationToken)
    {
        if (!logEvent.UserVisible) return ValueTask.CompletedTask;

        var previousColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = GetColor(logEvent.Level);
            Console.WriteLine(
                $"{logEvent.Timestamp.ToString(_timestampFormat)} [{logEvent.Level.Description()}] {logEvent.UserMessage}");
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }

        return ValueTask.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ConsoleColor GetColor(LogType level)
    {
        return level switch
        {
            LogType.Debug => ConsoleColor.Cyan,
            LogType.Info => ConsoleColor.Green,
            LogType.Warn => ConsoleColor.Yellow,
            LogType.Error or LogType.Fatal => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
    }
}
