using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Extensions.Logging;

public sealed class CodeWFLoggerOptions
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    public CodeWFFileLoggerOptions File { get; set; } = new();

    public CodeWFConsoleLoggerOptions Console { get; set; } = new();

    public CodeWFUserLogOptions UserLog { get; set; } = new();

    public CodeWFCaptureOptions Capture { get; set; } = new();

    public CodeWFQueueOptions Queue { get; set; } = new();

    internal LoggerOptions ToCoreOptions()
    {
        Validate();

        return new LoggerOptions
        {
            MinimumLevel = MinimumLevel,
            File = File.Enabled ? File.ToCoreOptions() : null,
            EnableConsole = Console.Enabled,
            Console = Console.Enabled ? Console.ToCoreOptions() : null,
            UserLogMode = UserLog.Mode,
            QueueCapacity = Queue.Capacity,
            RecentUserLogCapacity = UserLog.RecentCapacity
        };
    }

    internal void Validate()
    {
        if (!Enum.IsDefined(MinimumLevel))
            throw new ArgumentOutOfRangeException(nameof(MinimumLevel), "CodeWF 最低日志级别无效。");

        File.Validate();
        Console.Validate();
        UserLog.Validate();
        Queue.Validate();
    }
}

public sealed class CodeWFFileLoggerOptions
{
    public bool Enabled { get; set; } = true;

    public string DirectoryPath { get; set; } = "logs";

    public long MaxFileSizeBytes { get; set; } = 100L * 1024 * 1024;

    public int BatchSize { get; set; } = 200;

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    public string? OutputTemplate { get; set; }

    internal FileLogOptions ToCoreOptions()
    {
        return new FileLogOptions
        {
            DirectoryPath = ResolveLogDirectory(DirectoryPath),
            MaxFileSizeBytes = MaxFileSizeBytes,
            BatchSize = BatchSize,
            FlushInterval = FlushInterval,
            TimestampFormat = TimestampFormat,
            OutputTemplate = OutputTemplate
        };
    }

    internal void Validate()
    {
        if (!Enabled) return;
        ToCoreOptions().Validate();
    }

    private static string ResolveLogDirectory(string directoryPath)
    {
        var path = string.IsNullOrWhiteSpace(directoryPath)
            ? "logs"
            : directoryPath;

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
    }
}

public sealed class CodeWFConsoleLoggerOptions
{
    public bool Enabled { get; set; }

    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    public string? OutputTemplate { get; set; }

    internal ConsoleLogOptions ToCoreOptions()
    {
        return new ConsoleLogOptions
        {
            TimestampFormat = TimestampFormat,
            OutputTemplate = OutputTemplate,
            UserLogOnly = false
        };
    }

    internal void Validate()
    {
        if (!Enabled) return;
        ToCoreOptions().Validate();
    }
}

public sealed class CodeWFUserLogOptions
{
    public UserLogMode Mode { get; set; } = UserLogMode.ExplicitOnly;

    public int RecentCapacity { get; set; } = 2_000;

    internal void Validate()
    {
        if (!Enum.IsDefined(Mode))
            throw new ArgumentOutOfRangeException(nameof(Mode), "CodeWF 用户日志模式无效。");
        if (RecentCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(RecentCapacity), "CodeWF 用户日志缓存容量必须大于 0。");
    }
}

public sealed class CodeWFCaptureOptions
{
    public bool Scopes { get; set; } = true;

    public bool Activity { get; set; } = true;
}

public sealed class CodeWFQueueOptions
{
    public int Capacity { get; set; } = 10_000;

    internal void Validate()
    {
        if (Capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(Capacity), "CodeWF 日志队列容量必须大于 0。");
    }
}
