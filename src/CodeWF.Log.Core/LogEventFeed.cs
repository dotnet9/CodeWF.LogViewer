namespace CodeWF.Log.Core;

/// <summary>
/// Provides ordered recent-event replay and live delivery for complete log events.
/// </summary>
public sealed class LogEventFeed
{
    private readonly object _syncRoot = new();
    private readonly Queue<CodeWFLogEvent> _recentEvents = new();
    private readonly Dictionary<long, Subscriber> _subscribers = new();
    private readonly int _subscriberCapacity;
    private int _capacity;
    private long _nextSubscriptionId;
    private long _droppedSubscriberEventCount;

    public LogEventFeed(int capacity, ILineTemplateController lineTemplate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _subscriberCapacity = capacity;
        LineTemplate = lineTemplate ?? throw new ArgumentNullException(nameof(lineTemplate));
    }

    public ILineTemplateController LineTemplate { get; }

    /// <summary>Number of live events discarded from slow subscriber queues.</summary>
    public long DroppedSubscriberEventCount => Interlocked.Read(ref _droppedSubscriberEventCount);

    public IReadOnlyList<CodeWFLogEvent> GetRecentEvents()
    {
        lock (_syncRoot)
            return _recentEvents.ToArray();
    }

    public IDisposable Subscribe(Action<CodeWFLogEvent> handler, bool replayRecent = true)
    {
        ArgumentNullException.ThrowIfNull(handler);

        long id;
        Subscriber subscriber;
        lock (_syncRoot)
        {
            id = ++_nextSubscriptionId;
            subscriber = new Subscriber(handler, replayRecent ? _recentEvents.ToArray() : [], _subscriberCapacity);
            _subscribers.Add(id, subscriber);
        }

        subscriber.ScheduleDrain();
        return new Subscription(this, id);
    }

    internal void UpdateCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        lock (_syncRoot)
        {
            _capacity = capacity;
            while (_recentEvents.Count > _capacity) _recentEvents.Dequeue();
        }
    }

    internal void Publish(CodeWFLogEvent logEvent)
    {
        Subscriber[] subscribers;
        lock (_syncRoot)
        {
            _recentEvents.Enqueue(logEvent);
            while (_recentEvents.Count > _capacity) _recentEvents.Dequeue();
            subscribers = _subscribers.Values.ToArray();
            foreach (var subscriber in subscribers)
            {
                if (subscriber.Enqueue(logEvent))
                    Interlocked.Increment(ref _droppedSubscriberEventCount);
            }
        }

        foreach (var subscriber in subscribers) subscriber.ScheduleDrain();
    }

    private void Unsubscribe(long id)
    {
        Subscriber? subscriber;
        lock (_syncRoot)
        {
            if (!_subscribers.Remove(id, out subscriber)) return;
        }
        subscriber.Deactivate();
    }

    private sealed class Subscriber(
        Action<CodeWFLogEvent> handler,
        IEnumerable<CodeWFLogEvent> replay,
        int capacity)
    {
        private readonly object _syncRoot = new();
        private readonly Queue<CodeWFLogEvent> _pending = new(replay);
        private int _draining;
        private bool _active = true;

        public bool Enqueue(CodeWFLogEvent item)
        {
            lock (_syncRoot)
            {
                if (!_active) return false;
                var dropped = _pending.Count >= capacity;
                if (dropped) _pending.Dequeue();
                _pending.Enqueue(item);
                return dropped;
            }
        }

        public void ScheduleDrain()
        {
            if (Interlocked.Exchange(ref _draining, 1) != 0) return;
            ThreadPool.UnsafeQueueUserWorkItem(static subscriber => subscriber.Drain(), this, preferLocal: false);
        }

        public void Deactivate()
        {
            lock (_syncRoot)
            {
                _active = false;
                _pending.Clear();
            }
        }

        private void Drain()
        {
            do
            {
                while (true)
                {
                    CodeWFLogEvent item;
                    lock (_syncRoot)
                    {
                        if (!_active || _pending.Count == 0) break;
                        item = _pending.Dequeue();
                    }

                    try { handler(item); }
                    catch (Exception exception)
                    {
                        LoggerSelfDiagnostics.Report("日志事件订阅者处理失败。", exception);
                    }
                }

                Volatile.Write(ref _draining, 0);
                lock (_syncRoot)
                {
                    if (!_active || _pending.Count == 0 || Interlocked.Exchange(ref _draining, 1) != 0) return;
                }
            } while (true);
        }
    }

    private sealed class Subscription(LogEventFeed owner, long id) : IDisposable
    {
        private LogEventFeed? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unsubscribe(id);
    }
}
