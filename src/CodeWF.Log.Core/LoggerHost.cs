using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace CodeWF.Log.Core;

internal sealed class LoggerHost : IAsyncDisposable
{
    private readonly Channel<LogCommand> _commands;
    private readonly FileLogSink? _fileSink;
    private readonly ConsoleLogSink? _consoleSink;
    private readonly UserLogMode _userLogMode;
    private readonly Task _processingTask;
    private int _minimumLevel;
    private int _shutdownStarted;
    private long _sequence;

    public LoggerHost(LoggerOptions options, UserLogFeed? userLogs = null)
    {
        _minimumLevel = (int)options.MinimumLevel;
        _userLogMode = options.UserLogMode;
        UserLogs = userLogs ?? new UserLogFeed(options.RecentUserLogCapacity);
        UserLogs.UpdateCapacity(options.RecentUserLogCapacity);
        _fileSink = options.File is null ? null : new FileLogSink(options.File);
        var consoleOptions = options.Console ??
                             (options.EnableConsole
                                 ? new ConsoleLogOptions
                                 {
                                     TimestampFormat = options.File?.TimestampFormat ?? "yyyy-MM-dd HH:mm:ss.fff"
                                 }
                                 : null);
        _consoleSink = consoleOptions is null ? null : new ConsoleLogSink(consoleOptions);
        _commands = Channel.CreateBounded<LogCommand>(new BoundedChannelOptions(options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _processingTask = ProcessAsync();
    }

    public UserLogFeed UserLogs { get; }

    public LogLevel MinimumLevel
    {
        get => (LogLevel)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel, (int)value);
    }

    public void Write(LogLevel level, string message, Exception? exception, string? userMessage, bool userVisible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var normalizedUserMessage = string.IsNullOrWhiteSpace(userMessage) ? message : userMessage;
        var payload = userVisible
            ? new UserLogPayload { Message = normalizedUserMessage }
            : null;

        Write(new CodeWFLogEvent
        {
            Sequence = 0,
            Timestamp = DateTimeOffset.Now,
            Level = level,
            CategoryName = "CodeWF.Log.Logger",
            Message = message,
            UserLog = payload,
            Exception = exception
        });
    }

    public void Write(CodeWFLogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (!IsEnabled(logEvent.Level)) return;
        if (Volatile.Read(ref _shutdownStarted) != 0) return;

        var eventToWrite = logEvent.Sequence > 0
            ? logEvent
            : logEvent with { Sequence = Interlocked.Increment(ref _sequence) };

        Enqueue(new WriteLogCommand(eventToWrite));
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

    private bool IsEnabled(LogLevel level)
    {
        return level != LogLevel.None && (int)level >= Volatile.Read(ref _minimumLevel);
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
            // Logs written during shutdown should not break application shutdown.
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

    private async Task ProcessLogAsync(CodeWFLogEvent logEvent)
    {
        if (_fileSink is not null)
            await WriteSafelyAsync(_fileSink, logEvent, "写入日志文件失败。").ConfigureAwait(false);
        if (_consoleSink is not null)
            await WriteSafelyAsync(_consoleSink, logEvent, "输出控制台日志失败。").ConfigureAwait(false);

        var userEntry = CreateUserLogEntry(logEvent);
        if (userEntry is not null) UserLogs.Publish(userEntry);
    }

    private UserLogEntry? CreateUserLogEntry(CodeWFLogEvent logEvent)
    {
        if (_userLogMode == UserLogMode.Disabled) return null;

        var userMessage = logEvent.UserLog?.Message;
        var userProperties = logEvent.UserLog?.Properties ?? [];
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            if (_userLogMode != UserLogMode.FormattedMessage) return null;
            userMessage = logEvent.Message;
            userProperties = [];
        }

        if (string.IsNullOrWhiteSpace(userMessage)) return null;

        return new UserLogEntry
        {
            Sequence = logEvent.Sequence,
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level,
            Message = userMessage,
            CategoryName = logEvent.CategoryName,
            EventId = logEvent.EventId,
            TraceId = logEvent.TraceId,
            Properties = userProperties
                .Where(property => property.Visibility == LogPropertyVisibility.UserSafe)
                .ToArray()
        };
    }

    private static async Task WriteSafelyAsync(ILogSink sink, CodeWFLogEvent logEvent, string failureMessage)
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

    private sealed record WriteLogCommand(CodeWFLogEvent LogEvent) : LogCommand;

    private sealed record FlushLogCommand(TaskCompletionSource Completion) : LogCommand;
}
