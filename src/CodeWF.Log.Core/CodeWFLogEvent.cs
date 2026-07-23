using Microsoft.Extensions.Logging;

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

    public UserLogPayload? UserLog { get; init; }

    public Exception? Exception { get; init; }

    public IReadOnlyList<LogProperty> Properties { get; init; } = [];

    public IReadOnlyList<LogScope> Scopes { get; init; } = [];

    public string? TraceId { get; init; }

    public string? SpanId { get; init; }

    public string? ParentId { get; init; }

    public string? TraceState { get; init; }
}

public sealed record UserLogPayload
{
    public required string Message { get; init; }

    public IReadOnlyList<LogProperty> Properties { get; init; } = [];
}

public sealed record LogScope(
    string? Text,
    IReadOnlyList<LogProperty> Properties);

public sealed record LogProperty(
    string Name,
    LogValue Value,
    LogPropertyVisibility Visibility = LogPropertyVisibility.Diagnostic);

public enum LogPropertyVisibility
{
    Diagnostic,
    UserSafe
}

public abstract record LogValue;

public sealed record ScalarLogValue(object? Value) : LogValue;

public sealed record SequenceLogValue(IReadOnlyList<LogValue> Values) : LogValue;

public sealed record StructureLogValue(string? TypeName, IReadOnlyList<LogProperty> Properties) : LogValue;
