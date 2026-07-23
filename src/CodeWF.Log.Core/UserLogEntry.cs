using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

/// <summary>
/// 提供给界面、控制台和通知组件的用户安全日志条目。
/// </summary>
public sealed record UserLogEntry
{
    public required long Sequence { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required LogLevel Level { get; init; }

    public required string Message { get; init; }

    public string? CategoryName { get; init; }

    public EventId EventId { get; init; }

    public string? TraceId { get; init; }

    public IReadOnlyList<LogProperty> Properties { get; init; } = [];
}
