using Avalonia.Threading;
using CodeWF.Log.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWF.LogViewer.Avalonia;

internal sealed class LogViewSubscription : IDisposable
{
    private readonly ConcurrentQueue<UserLogEntry> _pendingEntries = new();
    private readonly Action<IReadOnlyList<UserLogEntry>> _receiveEntries;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancellationToken _cancellationToken;
    private readonly IDisposable _subscription;
    private long _refreshIntervalTicks;
    private int _dispatchScheduled;
    private int _disposed;

    public LogViewSubscription(
        UserLogFeed feed,
        Action<IReadOnlyList<UserLogEntry>> receiveEntries,
        TimeSpan refreshInterval)
    {
        _receiveEntries = receiveEntries;
        _refreshIntervalTicks = refreshInterval.Ticks;
        _cancellationToken = _cancellation.Token;
        _subscription = feed.Subscribe(OnLogReceived, replayRecent: true);
    }

    public void UpdateRefreshInterval(TimeSpan refreshInterval)
    {
        Interlocked.Exchange(ref _refreshIntervalTicks, refreshInterval.Ticks);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _subscription.Dispose();
        _cancellation.Cancel();
        _cancellation.Dispose();
        while (_pendingEntries.TryDequeue(out _))
        {
        }
    }

    private void OnLogReceived(UserLogEntry entry)
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        _pendingEntries.Enqueue(entry);
        TryScheduleDispatch();
    }

    private void TryScheduleDispatch()
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Interlocked.CompareExchange(ref _dispatchScheduled, 1, 0) != 0)
            return;

        // 强制建立异步边界，避免调度失败时在当前调用栈中同步重入。
        _ = Task.Run(DispatchAsync);
    }

    private async Task DispatchAsync()
    {
        try
        {
            if (Volatile.Read(ref _disposed) != 0) return;

            var refreshInterval = TimeSpan.FromTicks(Interlocked.Read(ref _refreshIntervalTicks));
            await Task.Delay(refreshInterval, _cancellationToken).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(Drain, DispatcherPriority.Background, _cancellationToken);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // 组件内部故障不能再次写入 Logger，否则可能形成日志递归。
            Trace.TraceError($"刷新 LogView 失败：{ex}");
            while (_pendingEntries.TryDequeue(out _))
            {
            }
        }
        finally
        {
            Interlocked.Exchange(ref _dispatchScheduled, 0);
        }

        if (!_pendingEntries.IsEmpty) TryScheduleDispatch();
    }

    private void Drain()
    {
        var entries = new List<UserLogEntry>();
        while (_pendingEntries.TryDequeue(out var entry)) entries.Add(entry);
        if (entries.Count > 0) _receiveEntries(entries);
    }
}
