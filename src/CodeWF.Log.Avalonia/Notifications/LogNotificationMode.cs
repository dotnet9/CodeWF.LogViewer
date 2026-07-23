namespace CodeWF.Log.Avalonia;

/// <summary>
/// 重要日志的通知方式。
/// </summary>
public enum LogNotificationMode
{
    /// <summary>
    /// 不弹出重要日志通知。
    /// </summary>
    None,

    /// <summary>
    /// 在当前 Avalonia TopLevel 内显示 Notification。
    /// </summary>
    InApp,

    /// <summary>
    /// 在桌面工作区右下角显示独立窗口。
    /// </summary>
    DesktopWindow
}
