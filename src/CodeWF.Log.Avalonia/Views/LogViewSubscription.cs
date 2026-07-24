using Avalonia.Threading;
using CodeWF.Log.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWF.Log.Avalonia;

internal sealed class LogViewSubscription : IDisposable
{
    private readonly ConcurrentQueue<CodeWFLogEvent> _pendingEntries = new();
    private readonly Action<IReadOnlyList<CodeWFLogEvent>> _receiveEntries;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly IDisposable _subscription;
    private long _refreshIntervalTicks;
    private int _dispatchScheduled;
    private int _disposed;

    public LogViewSubscription(
        LogEventFeed feed,
        Action<IReadOnlyList<CodeWFLogEvent>> receiveEntries,
        TimeSpan refreshInterval)
    {
        _receiveEntries = receiveEntries;
        _refreshIntervalTicks = refreshInterval.Ticks;
        _subscription = feed.Subscribe(OnLogReceived, replayRecent: true);
    }

    public void UpdateRefreshInterval(TimeSpan refreshInterval) =>
        Interlocked.Exchange(ref _refreshIntervalTicks, refreshInterval.Ticks);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _subscription.Dispose();
        _cancellation.Cancel();
        _cancellation.Dispose();
        while (_pendingEntries.TryDequeue(out _)) { }
    }

    private void OnLogReceived(CodeWFLogEvent entry)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        _pendingEntries.Enqueue(entry);
        TryScheduleDispatch();
    }

    private void TryScheduleDispatch()
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Interlocked.CompareExchange(ref _dispatchScheduled, 1, 0) != 0) return;
        _ = Task.Run(DispatchAsync);
    }

    private async Task DispatchAsync()
    {
        try
        {
            var delay = TimeSpan.FromTicks(Interlocked.Read(ref _refreshIntervalTicks));
            await Task.Delay(delay, _cancellation.Token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(Drain, DispatcherPriority.Background, _cancellation.Token);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Trace.TraceError($"刷新 LogView 失败：{ex}");
            while (_pendingEntries.TryDequeue(out _)) { }
        }
        finally { Interlocked.Exchange(ref _dispatchScheduled, 0); }

        if (!_pendingEntries.IsEmpty) TryScheduleDispatch();
    }

    private void Drain()
    {
        var entries = new List<CodeWFLogEvent>();
        while (_pendingEntries.TryDequeue(out var entry)) entries.Add(entry);
        if (entries.Count > 0) _receiveEntries(entries);
    }
}
