using System.Threading.Channels;

namespace CodeWF.Log.Core;

internal sealed class LoggerHost : IAsyncDisposable
{
    private readonly Channel<LogCommand> _commands;
    private readonly FileLogSink? _fileSink;
    private readonly ConsoleLogSink? _consoleSink;
    private readonly Task _processingTask;
    private int _minimumLevel;
    private int _shutdownStarted;
    private long _sequence;

    public LoggerHost(LoggerOptions options)
    {
        _minimumLevel = (int)options.MinimumLevel;
        UserLogs = new UserLogFeed(options.RecentUserLogCapacity);
        _fileSink = options.File is null ? null : new FileLogSink(options.File);
        _consoleSink = options.EnableConsole
            ? new ConsoleLogSink(options.File?.TimestampFormat ?? "yyyy-MM-dd HH:mm:ss.fff")
            : null;
        _commands = Channel.CreateBounded<LogCommand>(new BoundedChannelOptions(options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _processingTask = ProcessAsync();
    }

    public UserLogFeed UserLogs { get; }

    public LogType MinimumLevel
    {
        get => (LogType)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel, (int)value);
    }

    public void Write(LogType level, string message, Exception? exception, string? userMessage, bool userVisible)
    {
        if ((int)level < Volatile.Read(ref _minimumLevel)) return;
        if (Volatile.Read(ref _shutdownStarted) != 0) return;

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var normalizedUserMessage = string.IsNullOrWhiteSpace(userMessage) ? message : userMessage;
        var logEvent = new LogEvent(
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.Now,
            level,
            message,
            normalizedUserMessage,
            exception,
            userVisible);
        Enqueue(new WriteLogCommand(logEvent));
    }

    public async Task FlushAsync()
    {
        if (Volatile.Read(ref _shutdownStarted) != 0)
        {
            await _processingTask.ConfigureAwait(false);
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _commands.Writer.WriteAsync(new FlushLogCommand(completion)).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            await _processingTask.ConfigureAwait(false);
            return;
        }

        await completion.Task.ConfigureAwait(false);
    }

    public async Task ShutdownAsync()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            await _processingTask.ConfigureAwait(false);
            return;
        }

        _commands.Writer.TryComplete();
        await _processingTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
    }

    private void Enqueue(LogCommand command)
    {
        if (_commands.Writer.TryWrite(command)) return;

        try
        {
            _commands.Writer.WriteAsync(command).AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException)
        {
            // 进程关闭期间晚到的日志不应反向破坏业务退出流程。
        }
    }

    private async Task ProcessAsync()
    {
        await foreach (var command in _commands.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            switch (command)
            {
                case WriteLogCommand write:
                    await ProcessLogAsync(write.LogEvent).ConfigureAwait(false);
                    break;
                case FlushLogCommand flush:
                    await FlushSinksAsync(CancellationToken.None).ConfigureAwait(false);
                    flush.Completion.TrySetResult();
                    break;
            }
        }

        await FlushSinksAsync(CancellationToken.None).ConfigureAwait(false);
        if (_fileSink is not null) await _fileSink.DisposeAsync().ConfigureAwait(false);
        if (_consoleSink is not null) await _consoleSink.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ProcessLogAsync(LogEvent logEvent)
    {
        if (_fileSink is not null)
            await WriteSafelyAsync(_fileSink, logEvent, "写入日志文件失败。").ConfigureAwait(false);
        if (_consoleSink is not null)
            await WriteSafelyAsync(_consoleSink, logEvent, "输出控制台日志失败。").ConfigureAwait(false);

        if (logEvent.UserVisible)
            UserLogs.Publish(new UserLogEntry(
                logEvent.Sequence,
                logEvent.Timestamp,
                logEvent.Level,
                logEvent.UserMessage));
    }

    private static async Task WriteSafelyAsync(ILogSink sink, LogEvent logEvent, string failureMessage)
    {
        try
        {
            await sink.WriteAsync(logEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LoggerSelfDiagnostics.Report(failureMessage, ex);
        }
    }

    private async Task FlushSinksAsync(CancellationToken cancellationToken)
    {
        if (_fileSink is not null)
        {
            try
            {
                await _fileSink.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoggerSelfDiagnostics.Report("刷新日志文件失败。", ex);
            }
        }

        if (_consoleSink is not null)
            await _consoleSink.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private abstract record LogCommand;

    private sealed record WriteLogCommand(LogEvent LogEvent) : LogCommand;

    private sealed record FlushLogCommand(TaskCompletionSource Completion) : LogCommand;
}
