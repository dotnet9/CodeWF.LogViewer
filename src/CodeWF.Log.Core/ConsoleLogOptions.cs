namespace CodeWF.Log.Core;

public sealed record ConsoleLogOptions
{
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    public string? OutputTemplate { get; set; }

    internal bool UserLogOnly { get; init; } = true;

    internal void Validate()
    {
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
