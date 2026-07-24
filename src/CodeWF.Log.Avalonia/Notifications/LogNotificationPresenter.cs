using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using CodeWF.Log.Avalonia.Notifications.Views;
using CodeWF.Log.Core;
using CodeWF.Log.Core.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CodeWF.Log.Avalonia;

internal sealed class LogNotificationPresenter : IDisposable
{
    private const int MaxNotificationContentLength = 4_000;
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(10);

    private readonly Application _application;
    private readonly ConcurrentQueue<PendingNotification> _pendingNotifications = new();
    private IDisposable? _subscription;
    private LogEventFeed? _source;
    private IControlledApplicationLifetime? _controlledLifetime;
    private WindowNotificationManager? _inAppManager;
    private TopLevel? _inAppHost;
    private NotificationWindow? _desktopWindow;
    private IDataTemplate? _desktopContentTemplate;
    private string? _applicationName;
    private TimeSpan _duration = DefaultDuration;
    private LogNotificationMode _mode;
    private DesktopNotificationAttentionMode _attentionMode = DesktopNotificationAttentionMode.ShakeAndPulse;
    private int _minimumLevel = (int)LogLevel.Error;
    private int _maxVisibleCount = 3;
    private int _queueCapacity = 100;
    private int _generation;
    private int _pendingCount;
    private int _overflowCount;
    private int _dispatchScheduled;
    private int _disposed;

    public LogNotificationPresenter(Application application)
    {
        _application = application;
        LogNotificationResources.EnsureRegistered();
        TryAttachApplicationLifetime();
    }

    public void Configure(
        LogEventFeed source,
        LogNotificationMode mode,
        LogLevel minimumLevel,
        TimeSpan duration,
        int maxVisibleCount,
        int queueCapacity,
        string? applicationName,
        IDataTemplate? desktopContentTemplate,
        DesktopNotificationAttentionMode attentionMode)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        TryAttachApplicationLifetime();

        if (!ReferenceEquals(_source, source))
        {
            StopSubscription();
            _source = source;
            StartSubscription();
        }

        var modeChanged = _mode != mode;
        _mode = mode;
        Volatile.Write(ref _minimumLevel, (int)minimumLevel);
        _duration = duration < TimeSpan.Zero ? DefaultDuration : duration;
        _maxVisibleCount = Math.Max(1, maxVisibleCount);
        _queueCapacity = Math.Max(1, queueCapacity);
        if (_inAppManager is not null) _inAppManager.MaxItems = _maxVisibleCount;
        _applicationName = applicationName;
        _desktopContentTemplate = desktopContentTemplate;
        _attentionMode = attentionMode;

        if (modeChanged) { InvalidatePendingNotifications(); CloseNotifications(); }
        ConfigureDesktopWindow();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_controlledLifetime is not null) _controlledLifetime.Exit -= ApplicationLifetime_OnExit;
        StopSubscription();
        InvalidatePendingNotifications();
        CloseNotifications();
    }

    private void TryAttachApplicationLifetime()
    {
        if (_controlledLifetime is not null || _application.ApplicationLifetime is not IControlledApplicationLifetime lifetime) return;
        _controlledLifetime = lifetime;
        lifetime.Exit += ApplicationLifetime_OnExit;
    }

    private void ApplicationLifetime_OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) => Dispose();
    private void StartSubscription() => _subscription ??= _source?.Subscribe(OnLogReceived, replayRecent: false);
    private void StopSubscription() => Interlocked.Exchange(ref _subscription, null)?.Dispose();

    private void OnLogReceived(CodeWFLogEvent entry)
    {
        if (Volatile.Read(ref _disposed) != 0 || !Accepts(entry.Level)) return;
        if (Interlocked.Increment(ref _pendingCount) > Volatile.Read(ref _queueCapacity))
        {
            Interlocked.Decrement(ref _pendingCount);
            Interlocked.Increment(ref _overflowCount);
            ScheduleDispatch();
            return;
        }

        _pendingNotifications.Enqueue(new PendingNotification(entry, Volatile.Read(ref _generation)));
        ScheduleDispatch();
    }

    private bool Accepts(LogLevel level) =>
        level != LogLevel.None && (int)level >= Volatile.Read(ref _minimumLevel);

    private void ScheduleDispatch()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.CompareExchange(ref _dispatchScheduled, 1, 0) != 0) return;
        try { Dispatcher.UIThread.Post(DrainPendingNotifications); }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _dispatchScheduled, 0);
            ClearPendingNotifications();
            Trace.TraceError($"调度日志通知失败：{ex}");
        }
    }

    private void DrainPendingNotifications()
    {
        try
        {
            var entries = new List<(CodeWFLogEvent Entry, string Content)>();
            var generation = Volatile.Read(ref _generation);
            while (_pendingNotifications.TryDequeue(out var pending))
            {
                Interlocked.Decrement(ref _pendingCount);
                if (pending.Generation != generation || !Accepts(pending.Entry.Level)) continue;
                var template = _source?.LineTemplate.Current ?? LineTemplateController.DefaultTemplate;
                var content = LogTemplateFormatter.Format(pending.Entry, template, "yyyy-MM-dd HH:mm:ss.fff");
                entries.Add((pending.Entry, TrimContent(content)));
            }

            var overflow = Interlocked.Exchange(ref _overflowCount, 0);
            if (overflow > 0 && entries.Count > 0)
            {
                var last = entries[^1];
                entries[^1] = (last.Entry, $"{last.Content}{Environment.NewLine}另有 {overflow} 条日志，请在 LogView 中查看。");
            }

            if (entries.Count == 0) return;
            if (_mode == LogNotificationMode.DesktopWindow) ShowDesktopNotifications(entries);
            else if (_mode == LogNotificationMode.InApp)
                foreach (var entry in entries) ShowInAppNotification(entry.Entry, entry.Content);
        }
        catch (Exception ex) { ClearPendingNotifications(); Trace.TraceError($"显示日志通知失败：{ex}"); }
        finally
        {
            Interlocked.Exchange(ref _dispatchScheduled, 0);
            if (!_pendingNotifications.IsEmpty && Volatile.Read(ref _disposed) == 0) ScheduleDispatch();
        }
    }

    private void ShowInAppNotification(CodeWFLogEvent entry, string content)
    {
        var host = ResolveNotificationHost();
        if (host is null) return;
        if (!ReferenceEquals(_inAppHost, host))
        {
            _inAppManager?.CloseAll();
            _inAppManager = new WindowNotificationManager(host)
            {
                MaxItems = _maxVisibleCount,
                Position = NotificationPosition.TopRight
            };
            _inAppHost = host;
        }

        _inAppManager?.Show(new Notification(
            $"{GetApplicationName()} · {entry.Level.Description()} · {entry.Timestamp:HH:mm:ss}",
            content,
            GetNotificationType(entry.Level),
            _duration));
    }

    private void ShowDesktopNotifications(IReadOnlyList<(CodeWFLogEvent Entry, string Content)> entries)
    {
        if (_desktopWindow is null || _desktopWindow.IsClosing)
        {
            var owner = ResolveNotificationOwner();
            if (owner is null) return;
            var window = new NotificationWindow();
            window.Closed += (_, _) => { if (ReferenceEquals(_desktopWindow, window)) _desktopWindow = null; };
            _desktopWindow = window;
            ConfigureDesktopWindow();
            window.AddLogs(entries);
            window.Show(owner);
            return;
        }
        ConfigureDesktopWindow();
        _desktopWindow.AddLogs(entries);
    }

    private void ConfigureDesktopWindow() => _desktopWindow?.Configure(
        GetApplicationName(), _duration, ResolveNotificationHost(), _desktopContentTemplate, _attentionMode);

    private void InvalidatePendingNotifications() { Interlocked.Increment(ref _generation); ClearPendingNotifications(); }
    private void ClearPendingNotifications()
    {
        while (_pendingNotifications.TryDequeue(out _)) Interlocked.Decrement(ref _pendingCount);
        Interlocked.Exchange(ref _overflowCount, 0);
    }

    private void CloseNotifications()
    {
        _inAppManager?.CloseAll();
        _inAppManager = null;
        _inAppHost = null;
        _desktopWindow?.CloseNotification();
        _desktopWindow = null;
    }

    private TopLevel? ResolveNotificationHost()
    {
        if (_application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.Windows.LastOrDefault(window => window.IsActive && !ReferenceEquals(window, _desktopWindow)) ??
                   desktop.MainWindow ??
                   desktop.Windows.LastOrDefault(window => window.IsVisible && !ReferenceEquals(window, _desktopWindow));
        return _application.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView }
            ? TopLevel.GetTopLevel(mainView)
            : null;
    }

    private Window? ResolveNotificationOwner()
    {
        if (_application.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return null;
        if (desktop.MainWindow is { IsVisible: true } mainWindow) return mainWindow;
        return desktop.Windows.LastOrDefault(window => window.IsVisible && window.Owner is null && !ReferenceEquals(window, _desktopWindow));
    }

    private string GetApplicationName()
    {
        if (!string.IsNullOrWhiteSpace(_applicationName)) return _applicationName.Trim();
        using var process = Process.GetCurrentProcess();
        return process.ProcessName;
    }

    private static string TrimContent(string content) =>
        content.Length <= MaxNotificationContentLength ? content : content[..MaxNotificationContentLength] + "...";
    private static NotificationType GetNotificationType(LogLevel level) => level switch
    {
        LogLevel.Warning => NotificationType.Warning,
        LogLevel.Error or LogLevel.Critical => NotificationType.Error,
        _ => NotificationType.Information
    };

    private readonly record struct PendingNotification(CodeWFLogEvent Entry, int Generation);
}
