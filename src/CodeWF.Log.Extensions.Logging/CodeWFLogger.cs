using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CodeWF.Log.Extensions.Logging;

internal sealed class CodeWFLogger : ILogger
{
    private readonly string _categoryName;
    private readonly CodeWFLoggerProvider _provider;

    public CodeWFLogger(string categoryName, CodeWFLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _provider.ScopeProvider.Push(state);
    }

    public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        ArgumentNullException.ThrowIfNull(formatter);

        var options = _provider.Options;
        var stateSnapshot = LogStateSnapshot.Capture(state);
        var message = FormatMessage(state, exception, formatter);
        if (string.IsNullOrWhiteSpace(message) &&
            exception is null &&
            eventId.Id == 0 &&
            stateSnapshot.Properties.Count == 0 &&
            stateSnapshot.UserLog is null)
            return;

        var activity = options.Capture.Activity ? Activity.Current : null;
        _provider.Write(new CodeWFLogEvent
        {
            Sequence = 0,
            Timestamp = DateTimeOffset.Now,
            Level = logLevel,
            CategoryName = _categoryName,
            EventId = eventId,
            MessageTemplate = stateSnapshot.MessageTemplate,
            Message = message,
            UserLog = stateSnapshot.UserLog,
            Exception = exception,
            Properties = stateSnapshot.Properties,
            Scopes = options.Capture.Scopes ? CaptureScopes() : [],
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            ParentId = activity?.ParentId,
            TraceState = activity?.TraceStateString
        });
    }

    private IReadOnlyList<LogScope> CaptureScopes()
    {
        var scopes = new List<LogScope>();
        _provider.ScopeProvider.ForEachScope(static (scope, state) =>
        {
            state.Add(LogStateSnapshot.CaptureScope(scope));
        }, scopes);
        return scopes;
    }

    private static string FormatMessage<TState>(
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            return formatter(state, exception) ?? string.Empty;
        }
        catch (Exception ex)
        {
            LoggerSelfDiagnostics.Report("格式化 Microsoft.Extensions.Logging 消息失败。", ex);
            return state?.ToString() ?? string.Empty;
        }
    }
}
