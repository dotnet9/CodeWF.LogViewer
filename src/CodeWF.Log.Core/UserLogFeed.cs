namespace CodeWF.Log.Core;

/// <summary>
/// 支持历史回放和多订阅者的用户日志源。
/// </summary>
public sealed class UserLogFeed
{
    private readonly int _capacity;
    private readonly object _syncRoot = new();
    private readonly Queue<UserLogEntry> _recentEntries = new();
    private readonly Dictionary<long, Action<UserLogEntry>> _subscribers = new();
    private long _nextSubscriptionId;

    internal UserLogFeed(int capacity)
    {
        _capacity = capacity;
    }

    /// <summary>
    /// 获取当前缓存的最近用户日志。
    /// </summary>
    public IReadOnlyList<UserLogEntry> GetRecentEntries()
    {
        lock (_syncRoot)
        {
            return _recentEntries.ToArray();
        }
    }

    /// <summary>
    /// 订阅用户日志。日志查看器通常回放历史，通知组件应只订阅新日志。
    /// </summary>
    public IDisposable Subscribe(Action<UserLogEntry> handler, bool replayRecent = true)
    {
        ArgumentNullException.ThrowIfNull(handler);

        UserLogEntry[] recentEntries;
        long subscriptionId;
        lock (_syncRoot)
        {
            subscriptionId = ++_nextSubscriptionId;
            _subscribers.Add(subscriptionId, handler);
            recentEntries = replayRecent ? _recentEntries.ToArray() : [];
        }

        foreach (var entry in recentEntries)
            InvokeSubscriber(handler, entry);

        return new Subscription(this, subscriptionId);
    }

    internal void Publish(UserLogEntry entry)
    {
        Action<UserLogEntry>[] subscribers;
        lock (_syncRoot)
        {
            _recentEntries.Enqueue(entry);
            while (_recentEntries.Count > _capacity)
                _recentEntries.Dequeue();

            subscribers = _subscribers.Values.ToArray();
        }

        foreach (var subscriber in subscribers)
            InvokeSubscriber(subscriber, entry);
    }

    private static void InvokeSubscriber(Action<UserLogEntry> subscriber, UserLogEntry entry)
    {
        try
        {
            subscriber(entry);
        }
        catch
        {
            // 单个展示订阅方不能破坏日志主流程。
        }
    }

    private void Unsubscribe(long subscriptionId)
    {
        lock (_syncRoot)
        {
            _subscribers.Remove(subscriptionId);
        }
    }

    private sealed class Subscription(UserLogFeed owner, long subscriptionId) : IDisposable
    {
        private UserLogFeed? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Unsubscribe(subscriptionId);
        }
    }
}
