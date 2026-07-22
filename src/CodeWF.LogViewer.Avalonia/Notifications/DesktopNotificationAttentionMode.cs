namespace CodeWF.LogViewer.Avalonia;

/// <summary>
/// 重要日志通知窗口吸引用户注意的动效。
/// </summary>
public enum DesktopNotificationAttentionMode
{
    /// <summary>
    /// 不播放提醒动效。
    /// </summary>
    None,

    /// <summary>
    /// 仅播放日志级别图标脉冲。
    /// </summary>
    Pulse,

    /// <summary>
    /// Error 和 Fatal 播放窗口微抖及图标脉冲，Warn 仅播放图标脉冲。
    /// </summary>
    ShakeAndPulse
}
