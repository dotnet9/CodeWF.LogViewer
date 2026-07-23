using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CodeWF.Log.Extensions.Logging;

[ProviderAlias("CodeWF")]
public sealed class CodeWFLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, CodeWFLogger> _loggers = new(StringComparer.Ordinal);
    private readonly IDisposable? _optionsReload;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();
    private CodeWFLoggerOptions _options;
    private readonly bool _ownsCoreHost;
    private int _disposed;

    public CodeWFLoggerProvider(IOptionsMonitor<CodeWFLoggerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.CurrentValue;
        _ownsCoreHost = Logger.TryInitialize(_options.ToCoreOptions());
        _optionsReload = options.OnChange(UpdateOptions);
    }

    internal CodeWFLoggerOptions Options => _options;

    internal IExternalScopeProvider ScopeProvider => _scopeProvider;

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _loggers.GetOrAdd(categoryName, static (name, provider) => new CodeWFLogger(name, provider), this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _optionsReload?.Dispose();
        _loggers.Clear();

        if (_ownsCoreHost)
            Logger.ShutdownAsync().GetAwaiter().GetResult();
    }

    internal bool IsEnabled(LogLevel level)
    {
        var options = _options;
        return level != LogLevel.None && (int)level >= (int)options.MinimumLevel;
    }

    internal void Write(CodeWFLogEvent logEvent)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        Logger.Write(logEvent);
    }

    private void UpdateOptions(CodeWFLoggerOptions options)
    {
        options.Validate();
        _options = options;
        Logger.MinimumLevel = options.MinimumLevel;
    }
}
