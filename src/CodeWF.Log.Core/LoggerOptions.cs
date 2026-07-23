using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

/// <summary>
/// 日志组件初始化配置。
/// </summary>
public sealed record LoggerOptions
{
    /// <summary>
    /// Sink 级别的最低输出级别。MEL Provider 场景仍优先使用 MEL 自身过滤。
    /// </summary>
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// 文件日志配置；为 <see langword="null"/> 时不写文件。
    /// </summary>
    public FileLogOptions? File { get; init; }

    /// <summary>
    /// 是否把用户可见日志输出到控制台。
    /// </summary>
    public bool EnableConsole { get; init; } = true;

    public ConsoleLogOptions? Console { get; init; }

    /// <summary>
    /// 用户日志投影模式。
    /// </summary>
    public UserLogMode UserLogMode { get; init; } = UserLogMode.ExplicitOnly;

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
        if (!Enum.IsDefined(UserLogMode))
            throw new ArgumentOutOfRangeException(nameof(UserLogMode), "用户日志模式无效。");
        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity), "日志队列容量必须大于 0。");
        if (RecentUserLogCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(RecentUserLogCapacity), "用户日志缓存容量必须大于 0。");

        File?.Validate();
        Console?.Validate();
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
