namespace CodeWF.Log.Core;

/// <summary>
/// 内部完整日志事件。只有文件输出端可以读取异常和技术内容。
/// </summary>
internal sealed record LogEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    LogType Level,
    string Message,
    string UserMessage,
    Exception? Exception,
    bool UserVisible);
