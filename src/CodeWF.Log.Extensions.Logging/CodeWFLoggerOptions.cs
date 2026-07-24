using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Extensions.Logging;

public sealed class CodeWFLoggerOptions
{
    public string LineTemplate { get; set; } = LineTemplateController.DefaultTemplate;
    public bool BridgeStaticLogger { get; set; } = true;
    public CodeWFFileLoggerOptions File { get; set; } = new();
    public CodeWFConsoleLoggerOptions Console { get; set; } = new();
    public CodeWFEventFeedOptions EventFeed { get; set; } = new();
    public CodeWFCaptureOptions Capture { get; set; } = new();
    public CodeWFQueueOptions Queue { get; set; } = new();

    internal LoggerOptions ToCoreOptions(string contentRootPath)
    {
        Validate();
        return new LoggerOptions
        {
            MinimumLevel = LogLevel.Trace,
            LineTemplate = LineTemplate,
            File = File.Enabled ? File.ToCoreOptions(contentRootPath) : null,
            EnableConsole = Console.Enabled,
            Console = Console.Enabled ? Console.ToCoreOptions() : null,
            EnableEventFeed = EventFeed.Enabled,
            RecentEventCapacity = EventFeed.RecentCapacity,
            QueueCapacity = Queue.Capacity,
            QueueFullMode = Queue.FullMode,
            EnqueueTimeout = Queue.EnqueueTimeout
        };
    }

    internal void Validate()
    {
        if (!LogTemplateFormatter.TryValidate(LineTemplate, out var error))
            throw new ArgumentException(error, nameof(LineTemplate));
        File.Validate();
        Console.Validate();
        EventFeed.Validate();
        Queue.Validate();
    }
}

public sealed class CodeWFFileLoggerOptions
{
    public bool Enabled { get; set; } = true;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
    public string DirectoryPath { get; set; } = "logs";
    public long MaxFileSizeBytes { get; set; } = 1_000L * 1024 * 1024;
    public int RetentionDays { get; set; } = 30;
    public int? RetainedFileCountLimit { get; set; }
    public long? MaxDirectorySizeBytes { get; set; }
    public int BatchSize { get; set; } = 200;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";
    public string? OutputTemplate { get; set; }

    internal FileLogOptions ToCoreOptions(string contentRootPath) => new()
    {
        DirectoryPath = ResolveLogDirectory(DirectoryPath, contentRootPath),
        MinimumLevel = MinimumLevel,
        MaxFileSizeBytes = MaxFileSizeBytes,
        RetentionDays = RetentionDays,
        RetainedFileCountLimit = RetainedFileCountLimit,
        MaxDirectorySizeBytes = MaxDirectorySizeBytes,
        BatchSize = BatchSize,
        FlushInterval = FlushInterval,
        TimestampFormat = TimestampFormat,
        OutputTemplate = OutputTemplate
    };

    internal void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(DirectoryPath))
            throw new ArgumentException("日志目录不能为空。", nameof(DirectoryPath));
        ToCoreOptions(AppContext.BaseDirectory).Validate();
    }

    private static string ResolveLogDirectory(string path, string contentRootPath) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(contentRootPath, path));
}

public sealed class CodeWFConsoleLoggerOptions
{
    public bool Enabled { get; set; }
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    internal ConsoleLogOptions ToCoreOptions() => new()
    {
        MinimumLevel = MinimumLevel,
        TimestampFormat = TimestampFormat
    };

    internal void Validate()
    {
        if (Enabled) ToCoreOptions().Validate();
    }
}

public sealed class CodeWFEventFeedOptions
{
    public bool Enabled { get; set; } = true;
    public int RecentCapacity { get; set; } = 2_000;

    internal void Validate()
    {
        if (RecentCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(RecentCapacity), "事件缓存容量必须大于 0。");
    }
}

public sealed class CodeWFCaptureOptions
{
    public bool Scopes { get; set; } = true;
    public bool Activity { get; set; } = true;
    public bool ActivityTags { get; set; }
    public bool ActivityBaggage { get; set; }
}

public sealed class CodeWFQueueOptions
{
    public int Capacity { get; set; } = 10_000;
    public LogQueueFullMode FullMode { get; set; } = LogQueueFullMode.DropTraceAndDebug;
    public TimeSpan EnqueueTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

    internal void Validate()
    {
        if (Capacity <= 0) throw new ArgumentOutOfRangeException(nameof(Capacity), "日志队列容量必须大于 0。");
        if (!Enum.IsDefined(FullMode)) throw new ArgumentOutOfRangeException(nameof(FullMode), "日志队列满策略无效。");
        if (EnqueueTimeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(EnqueueTimeout), "入队超时不能小于 0。");
    }
}
