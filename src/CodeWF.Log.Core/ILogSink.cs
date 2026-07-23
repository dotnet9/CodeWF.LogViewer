namespace CodeWF.Log.Core;

internal interface ILogSink : IAsyncDisposable
{
    ValueTask WriteAsync(LogEvent logEvent, CancellationToken cancellationToken);

    Task FlushAsync(CancellationToken cancellationToken);
}
