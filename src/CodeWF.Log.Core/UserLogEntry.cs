namespace CodeWF.Log.Core;

/// <summary>
/// 提供给界面、控制台和通知组件的安全日志条目。
/// </summary>
public sealed record UserLogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    LogType Level,
    string Message);
