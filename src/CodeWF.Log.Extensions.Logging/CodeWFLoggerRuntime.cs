using CodeWF.Log.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CodeWF.Log.Extensions.Logging;

public sealed class CodeWFLoggerRuntime : IDisposable
{
    private readonly LoggerHost _host;
    private readonly bool _ownsStaticBridge;
    private int _disposed;

    public CodeWFLoggerRuntime(IOptions<CodeWFLoggerOptions> options, IServiceProvider services)
    {
        Options = options.Value;
        Options.Validate();
        CaptureScopes = Options.Capture.Scopes;
        CaptureActivity = Options.Capture.Activity;
        CaptureActivityTags = Options.Capture.ActivityTags;
        CaptureActivityBaggage = Options.Capture.ActivityBaggage;
        var contentRoot = services.GetService<IHostEnvironment>()?.ContentRootPath ?? AppContext.BaseDirectory;
        var coreOptions = Options.ToCoreOptions(contentRoot);
        LineTemplate = new LineTemplateController(coreOptions.LineTemplate);
        Events = new LogEventFeed(coreOptions.RecentEventCapacity, LineTemplate);
        Health = new CodeWFLogHealth();
        _host = new LoggerHost(coreOptions, Events, LineTemplate, Health);
        FileOutputTemplate = _host.FileOutputTemplate;
        _ownsStaticBridge = Options.BridgeStaticLogger && Logger.TryAttachHost(_host, Events, coreOptions, Health, this);
    }

    public CodeWFLoggerOptions Options { get; }
    public LogEventFeed Events { get; }
    public ILineTemplateController LineTemplate { get; }
    public IFileOutputTemplateController FileOutputTemplate { get; }
    public CodeWFLogHealth Health { get; }
    internal bool CaptureScopes { get; }
    internal bool CaptureActivity { get; }
    internal bool CaptureActivityTags { get; }
    internal bool CaptureActivityBaggage { get; }

    public void Write(CodeWFLogEvent logEvent)
    {
        if (Volatile.Read(ref _disposed) == 0) _host.Write(logEvent);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_ownsStaticBridge) Logger.DetachHost(this);
        _host.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
