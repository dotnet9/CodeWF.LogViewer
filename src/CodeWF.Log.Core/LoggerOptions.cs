namespace CodeWF.Log.Core;

/// <summary>
/// 日志组件初始化配置。
/// </summary>
public sealed record LoggerOptions
{
    /// <summary>
    /// 全局最低采集级别。低于此级别的日志不会进入任何输出端。
    /// </summary>
    public LogType MinimumLevel { get; init; } = LogType.Info;

    /// <summary>
    /// 文件日志配置；为 <see langword="null"/> 时不写文件。
    /// </summary>
    public FileLogOptions? File { get; init; }

    /// <summary>
    /// 是否输出用户日志到控制台。
    /// </summary>
    public bool EnableConsole { get; init; } = true;

    /// <summary>
    /// 后台日志队列容量。队列满时调用方会等待，日志不会被静默丢弃。
    /// </summary>
    public int QueueCapacity { get; init; } = 10_000;

    /// <summary>
    /// 为稍后创建的界面保留的最近用户日志数量。
    /// </summary>
    public int RecentUserLogCapacity { get; init; } = 2_000;

    internal void Validate()
    {
        if (!Enum.IsDefined(MinimumLevel))
            throw new ArgumentOutOfRangeException(nameof(MinimumLevel), "最低日志级别无效。");
        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity), "日志队列容量必须大于 0。");
        if (RecentUserLogCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(RecentUserLogCapacity), "用户日志缓存容量必须大于 0。");

        File?.Validate();
    }

    internal LoggerOptions Normalize()
    {
        Validate();
        return File is null
            ? this
            : this with
            {
                File = File with { DirectoryPath = Path.GetFullPath(File.DirectoryPath) }
            };
    }
}
