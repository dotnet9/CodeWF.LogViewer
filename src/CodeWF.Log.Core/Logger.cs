namespace CodeWF.Log.Core;

/// <summary>
/// 统一日志入口。普通方法写入文件和已启用的用户输出端，ToFile 方法只写文件。
/// </summary>
public static class Logger
{
    private static readonly object SyncRoot = new();
    private static LoggerHost? _host;
    private static LogType _minimumLevel = LogType.Info;

    internal static LoggerOptions? CurrentOptions { get; private set; }

    /// <summary>
    /// 全局最低采集级别。
    /// </summary>
    public static LogType MinimumLevel
    {
        get => Volatile.Read(ref _host)?.MinimumLevel ?? _minimumLevel;
        set
        {
            _minimumLevel = value;
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
    public static UserLogFeed UserLogs => GetHost().UserLogs;

    /// <summary>
    /// 初始化日志组件。一个进程生命周期内只能初始化一次。
    /// </summary>
    public static void Initialize(LoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options = options.Normalize();

        lock (SyncRoot)
        {
            if (_host is not null)
                throw new InvalidOperationException("日志组件已经初始化，不能重复初始化。");

            _minimumLevel = options.MinimumLevel;
            CurrentOptions = options;
            try
            {
                _host = new LoggerHost(options);
            }
            catch
            {
                CurrentOptions = null;
                throw;
            }
        }
    }

    public static void Log(LogType level, string message, string? userMessage = null) =>
        Write(level, message, null, userMessage, true);

    public static void Debug(string message, string? userMessage = null) =>
        Write(LogType.Debug, message, null, userMessage, true);

    public static void Info(string message, string? userMessage = null) =>
        Write(LogType.Info, message, null, userMessage, true);

    public static void Warn(string message) =>
        Write(LogType.Warn, message, null, null, true);

    public static void Warn(string message, string userMessage) =>
        Write(LogType.Warn, message, null, userMessage, true);

    public static void Warn(string message, Exception? exception, string? userMessage = null) =>
        Write(LogType.Warn, message, exception, userMessage, true);

    public static void Error(string message, Exception? exception = null, string? userMessage = null) =>
        Write(LogType.Error, message, exception, userMessage, true);

    public static void Fatal(string message, Exception? exception = null, string? userMessage = null) =>
        Write(LogType.Fatal, message, exception, userMessage, true);

    public static void DebugToFile(string message) =>
        Write(LogType.Debug, message, null, null, false);

    public static void InfoToFile(string message) =>
        Write(LogType.Info, message, null, null, false);

    public static void WarnToFile(string message, Exception? exception = null) =>
        Write(LogType.Warn, message, exception, null, false);

    public static void ErrorToFile(string message, Exception? exception = null) =>
        Write(LogType.Error, message, exception, null, false);

    public static void FatalToFile(string message, Exception? exception = null) =>
        Write(LogType.Fatal, message, exception, null, false);

    public static Task FlushAsync() => GetHost().FlushAsync();

    public static Task ShutdownAsync() => GetHost().ShutdownAsync();

    private static void Write(
        LogType level,
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
               throw new InvalidOperationException("日志组件尚未初始化，请先调用 Logger.Initialize。");
    }
}
