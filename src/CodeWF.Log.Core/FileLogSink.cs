using CodeWF.Log.Core.Extensions;
using System.Text;

namespace CodeWF.Log.Core;

internal sealed class FileLogSink : ILogSink
{
    private readonly FileLogOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _flushCancellation = new();
    private readonly Task _flushTask;
    private StreamWriter? _writer;
    private DateOnly _fileDate;
    private long _fileSize;
    private int _pendingCount;

    public FileLogSink(FileLogOptions options)
    {
        _options = options;
        Directory.CreateDirectory(_options.DirectoryPath);
        _flushTask = FlushPeriodicallyAsync(_flushCancellation.Token);
    }

    public async ValueTask WriteAsync(CodeWFLogEvent logEvent, CancellationToken cancellationToken)
    {
        var text = Format(logEvent);
        var byteCount = Encoding.UTF8.GetByteCount(text);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureWriterAsync(logEvent.Timestamp, byteCount, cancellationToken)
                .ConfigureAwait(false);
            await _writer!.WriteAsync(text.AsMemory(), cancellationToken).ConfigureAwait(false);
            _fileSize += byteCount;
            _pendingCount++;
            if (_pendingCount >= _options.BatchSize)
                await FlushCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await CloseWriterAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FlushCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _flushCancellation.CancelAsync().ConfigureAwait(false);
        try
        {
            await _flushTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await FlushCoreAsync(CancellationToken.None).ConfigureAwait(false);
            await CloseWriterAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
            _flushCancellation.Dispose();
        }
    }

    private async Task EnsureWriterAsync(DateTimeOffset timestamp, int incomingBytes, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(timestamp.LocalDateTime);
        if (_writer is not null && _fileDate == date && _fileSize + incomingBytes <= _options.MaxFileSizeBytes)
            return;

        await CloseWriterAsync().ConfigureAwait(false);
        Directory.CreateDirectory(_options.DirectoryPath);

        var filePath = GetAvailableFilePath(date, incomingBytes);
        var stream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        _writer = new StreamWriter(stream, new UTF8Encoding(false));
        _fileDate = date;
        _fileSize = stream.Length;
        _pendingCount = 0;

        cancellationToken.ThrowIfCancellationRequested();
    }

    private string GetAvailableFilePath(DateOnly date, int incomingBytes)
    {
        var baseName = $"Log_{date:yyyy_MM_dd}";
        for (var sequence = 0; ; sequence++)
        {
            var suffix = sequence == 0 ? string.Empty : $"_{sequence}";
            var filePath = Path.Combine(_options.DirectoryPath, $"{baseName}{suffix}.log");
            if (!File.Exists(filePath)) return filePath;

            var fileSize = new FileInfo(filePath).Length;
            if (fileSize == 0 || incomingBytes <= _options.MaxFileSizeBytes - fileSize) return filePath;
        }
    }

    private async Task FlushPeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.FlushInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LoggerSelfDiagnostics.Report("定时刷新日志文件失败。", ex);
            }
        }
    }

    private async Task FlushCoreAsync(CancellationToken cancellationToken)
    {
        if (_writer is null || _pendingCount == 0) return;

        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        _pendingCount = 0;
    }

    private async Task CloseWriterAsync()
    {
        if (_writer is null) return;

        await _writer.DisposeAsync().ConfigureAwait(false);
        _writer = null;
        _pendingCount = 0;
        _fileSize = 0;
    }

    private string Format(CodeWFLogEvent logEvent)
    {
        if (!string.IsNullOrWhiteSpace(_options.OutputTemplate))
        {
            var text = LogOutputTemplateFormatter.Format(
                logEvent,
                _options.OutputTemplate,
                _options.TimestampFormat);
            return text.EndsWith(Environment.NewLine, StringComparison.Ordinal)
                ? text
                : text + Environment.NewLine;
        }

        var builder = new StringBuilder();
        builder.Append(logEvent.Timestamp.ToString(_options.TimestampFormat))
            .Append(" [")
            .Append(logEvent.Level.Description())
            .Append("] ");

        if (!string.IsNullOrWhiteSpace(logEvent.CategoryName))
            builder.Append('(').Append(logEvent.CategoryName).Append(") ");

        if (logEvent.EventId.Id != 0 || !string.IsNullOrWhiteSpace(logEvent.EventId.Name))
            builder.Append("EventId=")
                .Append(logEvent.EventId.Id)
                .Append(string.IsNullOrWhiteSpace(logEvent.EventId.Name) ? string.Empty : $":{logEvent.EventId.Name}")
                .Append(' ');

        builder.AppendLine(logEvent.Message);

        if (!string.IsNullOrWhiteSpace(logEvent.MessageTemplate) &&
            !string.Equals(logEvent.MessageTemplate, logEvent.Message, StringComparison.Ordinal))
            builder.Append("Template: ").AppendLine(logEvent.MessageTemplate);

        if (logEvent.UserLog is not null &&
            !string.Equals(logEvent.UserLog.Message, logEvent.Message, StringComparison.Ordinal))
            builder.Append("用户提示: ").AppendLine(logEvent.UserLog.Message);

        AppendProperties(builder, "Properties", logEvent.Properties);
        AppendScopes(builder, logEvent.Scopes);
        AppendActivity(builder, logEvent);

        if (logEvent.Exception is not null)
            builder.AppendLine(logEvent.Exception.ToString());

        return builder.ToString();
    }

    private static void AppendProperties(StringBuilder builder, string title, IReadOnlyList<LogProperty> properties)
    {
        if (properties.Count == 0) return;

        builder.AppendLine($"{title}:");
        foreach (var property in properties)
        {
            builder.Append("  ")
                .Append(property.Name)
                .Append(" = ")
                .AppendLine(LogValueFormatter.ToDisplayString(property.Value));
        }
    }

    private static void AppendScopes(StringBuilder builder, IReadOnlyList<LogScope> scopes)
    {
        if (scopes.Count == 0) return;

        builder.AppendLine("Scopes:");
        foreach (var scope in scopes)
        {
            if (!string.IsNullOrWhiteSpace(scope.Text))
                builder.Append("  => ").AppendLine(scope.Text);
            AppendProperties(builder, "  ScopeProperties", scope.Properties);
        }
    }

    private static void AppendActivity(StringBuilder builder, CodeWFLogEvent logEvent)
    {
        if (string.IsNullOrWhiteSpace(logEvent.TraceId) &&
            string.IsNullOrWhiteSpace(logEvent.SpanId) &&
            string.IsNullOrWhiteSpace(logEvent.ParentId) &&
            string.IsNullOrWhiteSpace(logEvent.TraceState))
            return;

        builder.Append("Activity:");
        AppendActivityPart(builder, "TraceId", logEvent.TraceId);
        AppendActivityPart(builder, "SpanId", logEvent.SpanId);
        AppendActivityPart(builder, "ParentId", logEvent.ParentId);
        AppendActivityPart(builder, "TraceState", logEvent.TraceState);
        builder.AppendLine();
    }

    private static void AppendActivityPart(StringBuilder builder, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.Append(' ').Append(name).Append('=').Append(value);
    }
}
