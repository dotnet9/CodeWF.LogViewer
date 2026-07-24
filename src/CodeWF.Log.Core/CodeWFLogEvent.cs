using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CodeWF.Log.Core;

public sealed record CodeWFLogEvent
{
    public required long Sequence { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required LogLevel Level { get; init; }
    public required string CategoryName { get; init; }
    public EventId EventId { get; init; }
    public string? MessageTemplate { get; init; }
    public required string Message { get; init; }
    public string? UserMessage { get; init; }
    public LogExceptionInfo? Exception { get; init; }
    public IReadOnlyList<LogProperty> Properties { get; init; } = [];
    public IReadOnlyList<LogScope> Scopes { get; init; } = [];
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentId { get; init; }
    public string? TraceState { get; init; }
    public ActivityTraceFlags TraceFlags { get; init; }
    public IReadOnlyList<LogProperty> ActivityTags { get; init; } = [];
    public IReadOnlyList<LogProperty> ActivityBaggage { get; init; } = [];
}

public sealed record LogExceptionInfo
{
    private const int MaxDepth = 8;
    private const int MaxCount = 32;
    private const int MaxFieldLength = 32 * 1024;
    private const int MaxTotalLength = 64 * 1024;

    public required string TypeName { get; init; }
    public required string Message { get; init; }
    public required string Text { get; init; }
    public string? StackTrace { get; init; }
    public string? Source { get; init; }
    public int HResult { get; init; }
    public IReadOnlyList<LogExceptionInfo> InnerExceptions { get; init; } = [];

    public override string ToString() => Text;

    public static LogExceptionInfo? Capture(Exception? exception)
    {
        if (exception is null) return null;

        var remainingCount = MaxCount;
        var remainingCharacters = MaxTotalLength;
        return CaptureCore(exception, 0, ref remainingCount, ref remainingCharacters);
    }

    private static LogExceptionInfo CaptureCore(
        Exception exception,
        int depth,
        ref int remainingCount,
        ref int remainingCharacters)
    {
        remainingCount--;
        var innerExceptions = new List<LogExceptionInfo>();
        if (depth < MaxDepth && remainingCount > 0)
        {
            var source = exception is AggregateException aggregate
                ? aggregate.InnerExceptions
                : exception.InnerException is null ? [] : [exception.InnerException];
            foreach (var inner in source)
            {
                if (remainingCount <= 0 || remainingCharacters <= 0) break;
                innerExceptions.Add(CaptureCore(inner, depth + 1, ref remainingCount, ref remainingCharacters));
            }
        }

        return new LogExceptionInfo
        {
            TypeName = Take(exception.GetType().FullName ?? exception.GetType().Name, ref remainingCharacters),
            Message = Take(exception.Message, ref remainingCharacters),
            Text = Take(SafeToString(exception), ref remainingCharacters),
            StackTrace = TakeNullable(exception.StackTrace, ref remainingCharacters),
            Source = TakeNullable(exception.Source, ref remainingCharacters),
            HResult = exception.HResult,
            InnerExceptions = innerExceptions
        };
    }

    private static string SafeToString(Exception exception)
    {
        try
        {
            return exception.ToString();
        }
        catch
        {
            return $"{exception.GetType().FullName}: {exception.Message}";
        }
    }

    private static string Take(string value, ref int remainingCharacters)
    {
        var length = Math.Min(value.Length, Math.Min(MaxFieldLength, Math.Max(remainingCharacters, 0)));
        remainingCharacters -= length;
        return length == value.Length ? value : value[..length] + "…";
    }

    private static string? TakeNullable(string? value, ref int remainingCharacters) =>
        value is null ? null : Take(value, ref remainingCharacters);
}

public sealed record LogScope(string? Text, IReadOnlyList<LogProperty> Properties);

public sealed record LogProperty(string Name, LogValue Value);

public abstract record LogValue;

public sealed record ScalarLogValue(object? Value) : LogValue;

public sealed record SequenceLogValue(IReadOnlyList<LogValue> Values) : LogValue;

public sealed record StructureLogValue(string? TypeName, IReadOnlyList<LogProperty> Properties) : LogValue;
