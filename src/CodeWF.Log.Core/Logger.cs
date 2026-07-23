using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Core;

/// <summary>
/// 统一日志入口。普通方法写入文件和已启用的用户输出端，ToFile 方法只写诊断日志。
/// </summary>
public static class Logger
{
    private static readonly object SyncRoot = new();
    private static LoggerHost? _host;
    private static UserLogFeed? _userLogs;
    private static int _minimumLevel = (int)LogLevel.Information;

    internal static LoggerOptions? CurrentOptions { get; private set; }

    /// <summary>
    /// 全局最低输出级别。
    /// </summary>
    public static LogLevel MinimumLevel
    {
        get => Volatile.Read(ref _host)?.MinimumLevel ?? (LogLevel)Volatile.Read(ref _minimumLevel);
        set
        {
            ValidateLogLevel(value);
            Volatile.Write(ref _minimumLevel, (int)value);
            var host = Volatile.Read(ref _host);
            if (host is not null) host.MinimumLevel = value;
        }
    }

    /// <summary>
    /// 当前文件日志目录；未启用文件日志时为 <see langword="null"/>。
    /// </summary>
    public static string? LogDirectory => CurrentOptions?.File?.DirectoryPath;

    /// <summary>
    /// 用户安全日志源，供界面和通知组件订阅。
    /// </summary>
    public static UserLogFeed UserLogs => GetOrCreateUserLogs();

    /// <summary>
    /// 初始化日志组件。传统非 Host 场景可以继续使用；新项目优先使用 AddCodeWF()。
    /// </summary>
    public static void Initialize(LoggerOptions options)
    {
        if (!TryInitialize(options))
            throw new InvalidOperationException("日志组件已经初始化，不能重复初始化。");
    }

    /// <summary>
    /// 尝试初始化日志组件。已初始化时返回 <see langword="false"/>。
    /// </summary>
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
                _host = new LoggerHost(options, GetOrCreateUserLogs(options.RecentUserLogCapacity));
                return true;
            }
            catch
            {
                CurrentOptions = null;
                throw;
            }
        }
    }

    public static void Write(CodeWFLogEvent logEvent) => GetHost().Write(logEvent);

    public static void Log(LogLevel level, string message, string? userMessage = null) =>
        Write(level, message, null, userMessage, true);

    public static void Trace(string message, string? userMessage = null) =>
        Write(LogLevel.Trace, message, null, userMessage, true);

    public static void Debug(string message, string? userMessage = null) =>
        Write(LogLevel.Debug, message, null, userMessage, true);

    public static void Information(string message, string? userMessage = null) =>
        Write(LogLevel.Information, message, null, userMessage, true);

    public static void Info(string message, string? userMessage = null) =>
        Information(message, userMessage);

    public static void Warning(string message) =>
        Write(LogLevel.Warning, message, null, null, true);

    public static void Warning(string message, string userMessage) =>
        Write(LogLevel.Warning, message, null, userMessage, true);

    public static void Warning(string message, Exception? exception, string? userMessage = null) =>
        Write(LogLevel.Warning, message, exception, userMessage, true);

    public static void Warn(string message) => Warning(message);

    public static void Warn(string message, string userMessage) => Warning(message, userMessage);

    public static void Warn(string message, Exception? exception, string? userMessage = null) =>
        Warning(message, exception, userMessage);

    public static void Error(string message, Exception? exception = null, string? userMessage = null) =>
        Write(LogLevel.Error, message, exception, userMessage, true);

    public static void Critical(string message, Exception? exception = null, string? userMessage = null) =>
        Write(LogLevel.Critical, message, exception, userMessage, true);

    public static void Fatal(string message, Exception? exception = null, string? userMessage = null) =>
        Critical(message, exception, userMessage);

    public static void TraceToFile(string message) =>
        Write(LogLevel.Trace, message, null, null, false);

    public static void DebugToFile(string message) =>
        Write(LogLevel.Debug, message, null, null, false);

    public static void InformationToFile(string message) =>
        Write(LogLevel.Information, message, null, null, false);

    public static void InfoToFile(string message) =>
        InformationToFile(message);

    public static void WarningToFile(string message, Exception? exception = null) =>
        Write(LogLevel.Warning, message, exception, null, false);

    public static void WarnToFile(string message, Exception? exception = null) =>
        WarningToFile(message, exception);

    public static void ErrorToFile(string message, Exception? exception = null) =>
        Write(LogLevel.Error, message, exception, null, false);

    public static void CriticalToFile(string message, Exception? exception = null) =>
        Write(LogLevel.Critical, message, exception, null, false);

    public static void FatalToFile(string message, Exception? exception = null) =>
        CriticalToFile(message, exception);

    public static Task FlushAsync() => GetHost().FlushAsync();

    public static Task ShutdownAsync() => GetHost().ShutdownAsync();

    private static void Write(
        LogLevel level,
        string message,
        Exception? exception,
        string? userMessage,
        bool userVisible)
    {
        GetHost().Write(level, message, exception, userMessage, userVisible);
    }

    private static LoggerHost GetHost()
    {
        return Volatile.Read(ref _host) ??
               throw new InvalidOperationException("日志组件尚未初始化，请先调用 Logger.Initialize 或 AddCodeWF。");
    }

    private static UserLogFeed GetOrCreateUserLogs(int? capacity = null)
    {
        lock (SyncRoot)
        {
            if (_userLogs is null)
                _userLogs = new UserLogFeed(capacity ?? 2_000);
            else if (capacity.HasValue)
                _userLogs.UpdateCapacity(capacity.Value);

            return _userLogs;
        }
    }

    private static void ValidateLogLevel(LogLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level), "日志级别无效。");
    }
}
