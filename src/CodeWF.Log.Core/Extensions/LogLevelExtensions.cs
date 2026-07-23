using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core.Extensions;

public static class LogLevelExtensions
{
    public static string Description(this LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "跟踪",
            LogLevel.Debug => "调试",
            LogLevel.Information => "消息",
            LogLevel.Warning => "警告",
            LogLevel.Error => "错误",
            LogLevel.Critical => "严重错误",
            LogLevel.None => "不记录",
            _ => level.ToString()
        };
    }
}
