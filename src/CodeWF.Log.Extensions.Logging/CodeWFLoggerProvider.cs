using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace CodeWF.Log.Extensions.Logging;

[ProviderAlias("CodeWF")]
public sealed class CodeWFLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, CodeWFLogger> _loggers = new(StringComparer.Ordinal);
    private readonly CodeWFLoggerRuntime _runtime;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();
    private int _disposed;

    public CodeWFLoggerProvider(CodeWFLoggerRuntime runtime) => _runtime = runtime;

    internal IExternalScopeProvider ScopeProvider => _scopeProvider;
    internal bool CaptureScopes => _runtime.CaptureScopes;
    internal bool CaptureActivity => _runtime.CaptureActivity;
    internal bool CaptureActivityTags => _runtime.CaptureActivityTags;
    internal bool CaptureActivityBaggage => _runtime.CaptureActivityBaggage;

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _loggers.GetOrAdd(categoryName, static (name, provider) => new CodeWFLogger(name, provider), this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _loggers.Clear();
        _runtime.Dispose();
    }

    internal bool IsEnabled(LogLevel level) =>
        Volatile.Read(ref _disposed) == 0 && level != LogLevel.None;

    internal void Write(CodeWF.Log.Core.CodeWFLogEvent logEvent) => _runtime.Write(logEvent);
}
