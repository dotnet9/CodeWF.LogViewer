using CodeWF.Log.Core;
using System;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Avalonia;

/// <summary>
/// 重要日志通知窗口内容模板的数据源。
/// </summary>
public sealed class LogNotificationContent
{
    internal LogNotificationContent(string applicationName, CodeWFLogEvent logEntry, string content)
    {
        ApplicationName = applicationName;
        Level = logEntry.Level;
        RecordTime = logEntry.Timestamp.LocalDateTime;
        Content = content;
    }

    public string ApplicationName { get; }

    public LogLevel Level { get; }

    public DateTime RecordTime { get; }

    public string Content { get; }

}
