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
        DefaultContent = string.IsNullOrWhiteSpace(logEntry.UserMessage)
            ? logEntry.Message
            : logEntry.UserMessage.Trim();
    }

    public string ApplicationName { get; }

    public LogLevel Level { get; }

    public DateTime RecordTime { get; }

    public string Content { get; }

    /// <summary>
    /// 默认组合式桌面弹窗使用的正文。完整模板结果仍通过 <see cref="Content"/> 提供给自定义模板。
    /// </summary>
    internal string DefaultContent { get; }

}
