using CodeWF.Log.Core;
using System;

namespace CodeWF.LogViewer.Avalonia;

/// <summary>
/// 桌面重要日志窗口内容模板的数据源。
/// </summary>
public sealed class LogNotificationContent
{
    internal LogNotificationContent(string applicationName, LogInfo logInfo)
    {
        ApplicationName = applicationName;
        Level = logInfo.Level;
        RecordTime = logInfo.RecordTime;
        Content = string.IsNullOrWhiteSpace(logInfo.FriendlyDescription)
            ? logInfo.Description
            : logInfo.FriendlyDescription;
        OriginalContent = logInfo.Description;
    }

    public string ApplicationName { get; }

    public LogType Level { get; }

    public DateTime RecordTime { get; }

    public string Content { get; }

    public string OriginalContent { get; }
}
