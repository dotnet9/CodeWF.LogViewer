using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

public enum LogQueueFullMode
{
    Wait,
    DropNewest,
    DropTraceAndDebug
}

/// <summary>Core pipeline configuration.</summary>
public sealed record LoggerOptions
{
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
    public FileLogOptions? File { get; init; }
    public bool EnableConsole { get; init; } = true;
    public ConsoleLogOptions? Console { get; init; }
    public string LineTemplate { get; init; } = LineTemplateController.DefaultTemplate;
    public bool EnableEventFeed { get; init; } = true;
    public int QueueCapacity { get; init; } = 10_000;
    public LogQueueFullMode QueueFullMode { get; init; } = LogQueueFullMode.DropTraceAndDebug;
    public TimeSpan EnqueueTimeout { get; init; } = TimeSpan.FromMilliseconds(100);
    public int RecentEventCapacity { get; init; } = 2_000;

    internal void Validate()
    {
        if (!Enum.IsDefined(MinimumLevel))
            throw new ArgumentOutOfRangeException(nameof(MinimumLevel), "最低日志级别无效。");
        if (!Enum.IsDefined(QueueFullMode))
            throw new ArgumentOutOfRangeException(nameof(QueueFullMode), "队列满策略无效。");
        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity), "日志队列容量必须大于 0。");
        if (RecentEventCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(RecentEventCapacity), "日志事件缓存容量必须大于 0。");
        if (EnqueueTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(EnqueueTimeout), "入队超时不能小于 0。");
        if (!LogTemplateFormatter.TryValidate(LineTemplate, out var error))
            throw new ArgumentException(error, nameof(LineTemplate));

        File?.Validate();
        Console?.Validate();
    }

    internal LoggerOptions Normalize()
    {
        Validate();
        return File is null
            ? this
            : this with { File = File with { DirectoryPath = Path.GetFullPath(File.DirectoryPath) } };
    }
}
