namespace CodeWF.Log.Core;

using Microsoft.Extensions.Logging;

public sealed record ConsoleLogOptions
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    internal void Validate()
    {
        if (!Enum.IsDefined(MinimumLevel))
            throw new ArgumentOutOfRangeException(nameof(MinimumLevel), "控制台最低日志级别无效。");
        if (string.IsNullOrWhiteSpace(TimestampFormat))
            throw new ArgumentException("控制台日志时间格式不能为空。", nameof(TimestampFormat));

        try
        {
            _ = DateTimeOffset.Now.ToString(TimestampFormat);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("控制台日志时间格式不正确。", nameof(TimestampFormat), ex);
        }
    }
}
