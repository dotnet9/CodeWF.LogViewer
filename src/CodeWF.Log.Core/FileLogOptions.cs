namespace CodeWF.Log.Core;

/// <summary>
/// 文件日志配置。
/// </summary>
public sealed record FileLogOptions
{
    /// <summary>
    /// 最终日志目录。组件不会再自动追加子目录。
    /// </summary>
    public required string DirectoryPath { get; set; }

    /// <summary>
    /// 单个日志文件最大字节数。
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// 累积到此数量后立即刷新文件缓冲区。
    /// </summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>
    /// 文件缓冲区最长刷新间隔。
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 日志时间格式。
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// 文本输出模板；为空时使用 CodeWF 默认详细格式。
    /// </summary>
    public string? OutputTemplate { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(DirectoryPath))
            throw new ArgumentException("日志目录不能为空。", nameof(DirectoryPath));
        if (MaxFileSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxFileSizeBytes), "单个日志文件大小必须大于 0。");
        if (BatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "文件日志批量大小必须大于 0。");
        if (FlushInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(FlushInterval), "文件日志刷新间隔必须大于 0。");
        if (string.IsNullOrWhiteSpace(TimestampFormat))
            throw new ArgumentException("日志时间格式不能为空。", nameof(TimestampFormat));

        try
        {
            _ = DateTimeOffset.Now.ToString(TimestampFormat);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("日志时间格式不正确。", nameof(TimestampFormat), ex);
        }
    }
}
