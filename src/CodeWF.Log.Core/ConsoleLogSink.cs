using CodeWF.Log.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

internal sealed class ConsoleLogSink : ILogSink
{
    private static readonly object ConsoleSync = new();
    private readonly ConsoleLogOptions _options;
    private readonly ILineTemplateController _lineTemplate;

    public ConsoleLogSink(ConsoleLogOptions options, ILineTemplateController lineTemplate)
    {
        _options = options;
        _lineTemplate = lineTemplate;
    }

    public ValueTask WriteAsync(CodeWFLogEvent logEvent, CancellationToken cancellationToken)
    {
        lock (ConsoleSync)
        {
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
        }

        return ValueTask.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private string Format(CodeWFLogEvent logEvent)
    {
        var text = LogTemplateFormatter.Format(logEvent, _lineTemplate.Current, _options.TimestampFormat);
        return text.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? text
            : text + Environment.NewLine;
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
