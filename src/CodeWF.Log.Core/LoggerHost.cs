using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace CodeWF.Log.Core;

internal sealed class LoggerHost : IAsyncDisposable
{
    private readonly Channel<LogCommand> _commands;
    private readonly FileLogSink? _fileSink;
    private readonly ConsoleLogSink? _consoleSink;
    private readonly LoggerOptions _options;
    private readonly Task _processingTask;
    private int _minimumLevel;
    private int _shutdownStarted;
    private long _sequence;

    public LoggerHost(
        LoggerOptions options,
        LogEventFeed? eventFeed = null,
        ILineTemplateController? lineTemplate = null,
        CodeWFLogHealth? health = null)
    {
        _options = options.Normalize();
        _minimumLevel = (int)_options.MinimumLevel;
        LineTemplate = lineTemplate ?? eventFeed?.LineTemplate ?? new LineTemplateController(_options.LineTemplate);
        FileOutputTemplate = new FileOutputTemplateController(_options.File?.OutputTemplate);
        Events = eventFeed ?? new LogEventFeed(_options.RecentEventCapacity, LineTemplate);
        Health = health ?? new CodeWFLogHealth();
        Events.UpdateCapacity(_options.RecentEventCapacity);
        _fileSink = _options.File is null ? null : new FileLogSink(_options.File, FileOutputTemplate);
        var consoleOptions = _options.Console ??
                             (_options.EnableConsole
                                 ? new ConsoleLogOptions
                                 {
                                     TimestampFormat = _options.File?.TimestampFormat ?? "yyyy-MM-dd HH:mm:ss.fff"
                                 }
                                 : null);
        _consoleSink = consoleOptions is null ? null : new ConsoleLogSink(consoleOptions, LineTemplate);
        _commands = Channel.CreateBounded<LogCommand>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _processingTask = ProcessAsync();
    }

    public LogEventFeed Events { get; }
    public ILineTemplateController LineTemplate { get; }
    public IFileOutputTemplateController FileOutputTemplate { get; }
    public CodeWFLogHealth Health { get; }

    public LogLevel MinimumLevel
    {
        get => (LogLevel)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel, (int)value);
    }

    public void Write(CodeWFLogEvent logEvent, bool fileOnly = false)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (!IsEnabled(logEvent.Level) || Volatile.Read(ref _shutdownStarted) != 0) return;
        EnqueueLog(new WriteLogCommand(logEvent with { Sequence = 0 }, fileOnly));
    }

    public async Task FlushAsync()
    {
        if (Volatile.Read(ref _shutdownStarted) != 0)
        {
            await _processingTask.ConfigureAwait(false);
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try { await _commands.Writer.WriteAsync(new FlushLogCommand(completion)).ConfigureAwait(false); }
        catch (ChannelClosedException) { await _processingTask.ConfigureAwait(false); return; }
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

    public async ValueTask DisposeAsync() => await ShutdownAsync().ConfigureAwait(false);

    private bool IsEnabled(LogLevel level) =>
        level != LogLevel.None && (int)level >= Volatile.Read(ref _minimumLevel);

    private void EnqueueLog(WriteLogCommand command)
    {
        if (_commands.Writer.TryWrite(command)) return;
        if (_options.QueueFullMode == LogQueueFullMode.DropNewest ||
            (_options.QueueFullMode == LogQueueFullMode.DropTraceAndDebug &&
             command.LogEvent.Level is LogLevel.Trace or LogLevel.Debug))
        {
            Health.RecordDropped(command.LogEvent.Level);
            return;
        }

        try
        {
            if (_options.QueueFullMode == LogQueueFullMode.Wait)
            {
                _commands.Writer.WriteAsync(command).AsTask().GetAwaiter().GetResult();
                return;
            }

            using var timeout = new CancellationTokenSource(_options.EnqueueTimeout);
            _commands.Writer.WriteAsync(command, timeout.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { Health.RecordDropped(command.LogEvent.Level); }
        catch (ChannelClosedException) { }
    }

    private async Task ProcessAsync()
    {
        await foreach (var command in _commands.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            switch (command)
            {
                case WriteLogCommand write:
                    var sequenced = write.LogEvent with { Sequence = ++_sequence };
                    await ProcessLogAsync(sequenced, write.FileOnly).ConfigureAwait(false);
                    break;
                case FlushLogCommand flush:
                    await FlushSinksAsync(CancellationToken.None).ConfigureAwait(false);
                    flush.Completion.TrySetResult();
                    break;
            }
        }

        await FlushSinksAsync(CancellationToken.None).ConfigureAwait(false);
        await DisposeSinkSafelyAsync(_fileSink, "释放日志文件失败。").ConfigureAwait(false);
        await DisposeSinkSafelyAsync(_consoleSink, "释放控制台日志失败。").ConfigureAwait(false);
    }

    private async Task ProcessLogAsync(CodeWFLogEvent logEvent, bool fileOnly)
    {
        if (_fileSink is not null && _options.File is { } file && logEvent.Level >= file.MinimumLevel)
            await WriteSafelyAsync(_fileSink, logEvent, "写入日志文件失败。").ConfigureAwait(false);

        if (fileOnly) return;

        if (_consoleSink is not null)
        {
            var minimumLevel = _options.Console?.MinimumLevel ?? LogLevel.Trace;
            if (logEvent.Level >= minimumLevel)
                await WriteSafelyAsync(_consoleSink, logEvent, "输出控制台日志失败。").ConfigureAwait(false);
        }

        if (_options.EnableEventFeed) Events.Publish(logEvent);
    }

    private static async Task WriteSafelyAsync(ILogSink sink, CodeWFLogEvent logEvent, string failureMessage)
    {
        try { await sink.WriteAsync(logEvent, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { LoggerSelfDiagnostics.Report(failureMessage, ex); }
    }

    private async Task FlushSinksAsync(CancellationToken cancellationToken)
    {
        if (_fileSink is not null)
        {
            try { await _fileSink.FlushAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { LoggerSelfDiagnostics.Report("刷新日志文件失败。", ex); }
        }

        if (_consoleSink is not null) await _consoleSink.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DisposeSinkSafelyAsync(ILogSink? sink, string failureMessage)
    {
        if (sink is null) return;
        try { await sink.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { LoggerSelfDiagnostics.Report(failureMessage, ex); }
    }

    private abstract record LogCommand;
    private sealed record WriteLogCommand(CodeWFLogEvent LogEvent, bool FileOnly) : LogCommand;
    private sealed record FlushLogCommand(TaskCompletionSource Completion) : LogCommand;
}
