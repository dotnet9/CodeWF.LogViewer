using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

/// <summary>Legacy static logging facade.</summary>
public static class Logger
{
    private static readonly object SyncRoot = new();
    private static readonly object LegacyOwner = new();
    private static LoggerHost? _host;
    private static object? _hostOwner;
    private static LogEventFeed? _events;
    private static CodeWFLogHealth? _health;
    private static int _minimumLevel = (int)LogLevel.Information;

    internal static LoggerOptions? CurrentOptions { get; private set; }

    public static LogLevel MinimumLevel
    {
        get => Volatile.Read(ref _host)?.MinimumLevel ?? (LogLevel)Volatile.Read(ref _minimumLevel);
        set
        {
            ValidateLogLevel(value);
            Volatile.Write(ref _minimumLevel, (int)value);
            if (Volatile.Read(ref _host) is { } host) host.MinimumLevel = value;
        }
    }

    public static LogLevel Level { get => MinimumLevel; set => MinimumLevel = value; }
    public static string? LogDirectory => CurrentOptions?.File?.DirectoryPath;
    public static LogEventFeed Events => GetOrCreateEvents();
    public static IFileOutputTemplateController FileOutputTemplate => GetHost().FileOutputTemplate;
    public static CodeWFLogHealth Health
    {
        get { lock (SyncRoot) return _health ??= new CodeWFLogHealth(); }
    }

    public static void Initialize(LoggerOptions options)
    {
        if (!TryInitialize(options))
            throw new InvalidOperationException("日志组件已经初始化，不能重复初始化。");
    }

    public static bool TryInitialize(LoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options = options.Normalize();
        lock (SyncRoot)
        {
            if (_host is not null) return false;
            Volatile.Write(ref _minimumLevel, (int)options.MinimumLevel);
            CurrentOptions = options;
            try
            {
                var controller = new LineTemplateController(options.LineTemplate);
                _events = new LogEventFeed(options.RecentEventCapacity, controller);
                _health = new CodeWFLogHealth();
                _host = new LoggerHost(options, _events, controller, _health);
                _hostOwner = LegacyOwner;
                return true;
            }
            catch
            {
                CurrentOptions = null;
                _events = null;
                _health = null;
                _hostOwner = null;
                throw;
            }
        }
    }

    public static void Write(CodeWFLogEvent logEvent) => GetHost().Write(logEvent);
    public static void Log(LogLevel level, string message, string? userMessage = null) => Write(level, message, null, userMessage);
    public static void Trace(string message, string? userMessage = null) => Write(LogLevel.Trace, message, null, userMessage);
    public static void Debug(string message, string? userMessage = null) => Write(LogLevel.Debug, message, null, userMessage);
    public static void Information(string message, string? userMessage = null) => Write(LogLevel.Information, message, null, userMessage);
    public static void Info(string message, string? userMessage = null) => Information(message, userMessage);
    public static void Warning(string message) => Write(LogLevel.Warning, message, null, null);
    public static void Warning(string message, string userMessage) => Write(LogLevel.Warning, message, null, userMessage);
    public static void Warning(string message, Exception? exception, string? userMessage = null) => Write(LogLevel.Warning, message, exception, userMessage);
    public static void Warn(string message) => Warning(message);
    public static void Warn(string message, string userMessage) => Warning(message, userMessage);
    public static void Warn(string message, Exception? exception, string? userMessage = null) => Warning(message, exception, userMessage);
    public static void Error(string message, Exception? exception = null, string? userMessage = null) => Write(LogLevel.Error, message, exception, userMessage);
    public static void Critical(string message, Exception? exception = null, string? userMessage = null) => Write(LogLevel.Critical, message, exception, userMessage);
    public static void Fatal(string message, Exception? exception = null, string? userMessage = null) => Critical(message, exception, userMessage);

    public static void TraceToFile(string message) => Write(LogLevel.Trace, message, null, null, true);
    public static void DebugToFile(string message) => Write(LogLevel.Debug, message, null, null, true);
    public static void InformationToFile(string message) => Write(LogLevel.Information, message, null, null, true);
    public static void InfoToFile(string message) => InformationToFile(message);
    public static void WarningToFile(string message, Exception? exception = null) => Write(LogLevel.Warning, message, exception, null, true);
    public static void WarnToFile(string message, Exception? exception = null) => WarningToFile(message, exception);
    public static void ErrorToFile(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception, null, true);
    public static void CriticalToFile(string message, Exception? exception = null) => Write(LogLevel.Critical, message, exception, null, true);
    public static void FatalToFile(string message, Exception? exception = null) => CriticalToFile(message, exception);

    public static Task FlushAsync() => GetHost().FlushAsync();

    public static async Task ShutdownAsync()
    {
        LoggerHost host;
        bool ownsHost;
        lock (SyncRoot)
        {
            host = _host ?? throw new InvalidOperationException("日志组件尚未初始化，请先调用 Logger.Initialize 或 AddCodeWF。");
            ownsHost = ReferenceEquals(_hostOwner, LegacyOwner);
        }

        if (!ownsHost)
        {
            // DI Provider owns its pipeline lifetime. Static callers may flush it, but must not dispose it.
            await host.FlushAsync().ConfigureAwait(false);
            return;
        }

        await host.ShutdownAsync().ConfigureAwait(false);
        lock (SyncRoot)
        {
            if (!ReferenceEquals(_host, host)) return;
            _host = null;
            _hostOwner = null;
            _events = null;
            _health = null;
            CurrentOptions = null;
        }
    }

    internal static bool TryAttachHost(
        LoggerHost host,
        LogEventFeed events,
        LoggerOptions options,
        CodeWFLogHealth health,
        object owner)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(owner);
        lock (SyncRoot)
        {
            if (_host is not null) return false;
            _host = host;
            _hostOwner = owner;
            _events = events;
            _health = health;
            CurrentOptions = options;
            Volatile.Write(ref _minimumLevel, (int)options.MinimumLevel);
            return true;
        }
    }

    internal static void DetachHost(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (SyncRoot)
        {
            if (!ReferenceEquals(_hostOwner, owner)) return;
            _host = null;
            _hostOwner = null;
            _events = null;
            _health = null;
            CurrentOptions = null;
        }
    }

    private static void Write(LogLevel level, string message, Exception? exception, string? userMessage, bool fileOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        GetHost().Write(new CodeWFLogEvent
        {
            Sequence = 0,
            Timestamp = DateTimeOffset.Now,
            Level = level,
            CategoryName = "CodeWF.Log.Logger",
            Message = message,
            UserMessage = userMessage,
            Exception = LogExceptionInfo.Capture(exception)
        }, fileOnly);
    }

    private static LoggerHost GetHost() => Volatile.Read(ref _host) ??
        throw new InvalidOperationException("日志组件尚未初始化，请先调用 Logger.Initialize 或 AddCodeWF。");

    private static LogEventFeed GetOrCreateEvents()
    {
        lock (SyncRoot)
            return _events ??= new LogEventFeed(2_000, new LineTemplateController());
    }

    private static void ValidateLogLevel(LogLevel level)
    {
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level), "日志级别无效。");
    }
}
