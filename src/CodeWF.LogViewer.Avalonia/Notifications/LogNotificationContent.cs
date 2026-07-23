using CodeWF.Log.Core;
using System;

namespace CodeWF.LogViewer.Avalonia;

/// <summary>
/// 重要日志通知窗口内容模板的数据源。
/// </summary>
public sealed class LogNotificationContent
{
    internal LogNotificationContent(string applicationName, UserLogEntry logEntry)
    {
        ApplicationName = applicationName;
        Level = logEntry.Level;
        RecordTime = logEntry.Timestamp.LocalDateTime;
        Content = logEntry.Message;
    }

    public string ApplicationName { get; }

    public LogType Level { get; }

    public DateTime RecordTime { get; }

    public string Content { get; }

}
