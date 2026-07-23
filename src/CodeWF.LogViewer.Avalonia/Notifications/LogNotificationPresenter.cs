using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using CodeWF.Log.Core;
using CodeWF.Log.Core.Extensions;
using CodeWF.LogViewer.Avalonia.Notifications.Views;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CodeWF.LogViewer.Avalonia;

internal sealed class LogNotificationPresenter : IDisposable
{
    private const int MaxPendingNotificationCount = 100;
    private const int MaxNotificationContentLength = 500;
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(10);

    private readonly Application _application;
    private readonly ConcurrentQueue<PendingNotification> _pendingNotifications = new();
    private IDisposable? _subscription;
    private IControlledApplicationLifetime? _controlledLifetime;
    private WindowNotificationManager? _inAppManager;
    private TopLevel? _inAppHost;
    private NotificationWindow? _desktopWindow;
    private IDataTemplate? _desktopContentTemplate;
    private string? _applicationName;
    private TimeSpan _duration = DefaultDuration;
    private LogNotificationMode _mode;
    private DesktopNotificationAttentionMode _attentionMode = DesktopNotificationAttentionMode.ShakeAndPulse;
    private int _minimumLevel = (int)LogType.Error;
    private int _maximumLevel = (int)LogType.Fatal;
    private int _generation;
    private int _pendingCount;
    private int _dispatchScheduled;
    private int _disposed;

    public LogNotificationPresenter(Application application)
    {
        _application = application;
        LogNotificationResources.EnsureRegistered();
        TryAttachApplicationLifetime();
        StartSubscription();
    }

    public void Configure(
        LogNotificationMode mode,
        LogType minimumLevel,
        LogType maximumLevel,
        TimeSpan duration,
        string? applicationName,
        IDataTemplate? desktopContentTemplate,
        DesktopNotificationAttentionMode attentionMode)
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        TryAttachApplicationLifetime();
        var modeChanged = _mode != mode;
        _mode = mode;
        Volatile.Write(ref _minimumLevel, (int)minimumLevel);
        Volatile.Write(ref _maximumLevel, (int)maximumLevel);
        _duration = duration < TimeSpan.Zero ? DefaultDuration : duration;
        _applicationName = applicationName;
        _desktopContentTemplate = desktopContentTemplate;
        _attentionMode = attentionMode;

        if (modeChanged)
        {
            InvalidatePendingNotifications();
            CloseNotifications();
        }

        ConfigureDesktopWindow();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (_controlledLifetime != null) _controlledLifetime.Exit -= ApplicationLifetime_OnExit;
        _controlledLifetime = null;
        StopSubscription();
        InvalidatePendingNotifications();
        CloseNotifications();
    }

    private void TryAttachApplicationLifetime()
    {
        if (_controlledLifetime != null ||
            _application.ApplicationLifetime is not IControlledApplicationLifetime lifetime)
            return;

        _controlledLifetime = lifetime;
        lifetime.Exit += ApplicationLifetime_OnExit;
    }

    private void ApplicationLifetime_OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) => Dispose();

    private void StartSubscription()
    {
        if (Volatile.Read(ref _disposed) == 0)
            _subscription ??= Logger.UserLogs.Subscribe(OnLogReceived, replayRecent: false);
    }

    private void StopSubscription()
    {
        Interlocked.Exchange(ref _subscription, null)?.Dispose();
    }

    private void OnLogReceived(UserLogEntry entry)
    {
        if (Volatile.Read(ref _disposed) != 0 || !Accepts(entry.Level)) return;

        var generation = Volatile.Read(ref _generation);
        _pendingNotifications.Enqueue(new PendingNotification(entry, generation));
        Interlocked.Increment(ref _pendingCount);
        while (Volatile.Read(ref _pendingCount) > MaxPendingNotificationCount &&
               _pendingNotifications.TryDequeue(out _))
            Interlocked.Decrement(ref _pendingCount);

        ScheduleDispatch();
    }

    private bool Accepts(LogType level)
    {
        var minimumLevel = Volatile.Read(ref _minimumLevel);
        var maximumLevel = Volatile.Read(ref _maximumLevel);
        return minimumLevel <= maximumLevel && (int)level >= minimumLevel && (int)level <= maximumLevel;
    }

    private void ScheduleDispatch()
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Interlocked.CompareExchange(ref _dispatchScheduled, 1, 0) != 0)
            return;

        try
        {
            Dispatcher.UIThread.Post(DrainPendingNotifications);
        }
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
            if (Volatile.Read(ref _disposed) != 0) return;
            TryAttachApplicationLifetime();

            var entries = new List<UserLogEntry>();
            var generation = Volatile.Read(ref _generation);
            while (_pendingNotifications.TryDequeue(out var pending))
            {
                Interlocked.Decrement(ref _pendingCount);
                if (pending.Generation == generation && Accepts(pending.Entry.Level)) entries.Add(pending.Entry);
            }

            if (entries.Count == 0) return;
            if (_mode == LogNotificationMode.DesktopWindow)
            {
                ShowDesktopNotifications(entries);
                return;
            }

            if (_mode == LogNotificationMode.InApp)
                foreach (var entry in entries) ShowInAppNotification(entry);
        }
        catch (Exception ex)
        {
            ClearPendingNotifications();
            Trace.TraceError($"显示日志通知失败：{ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _dispatchScheduled, 0);
            if (!_pendingNotifications.IsEmpty && Volatile.Read(ref _disposed) == 0) ScheduleDispatch();
        }
    }

    private void ShowInAppNotification(UserLogEntry entry)
    {
        var host = ResolveNotificationHost();
        if (host is null) return;

        if (!ReferenceEquals(_inAppHost, host))
        {
            _inAppManager?.CloseAll();
            _inAppManager = new WindowNotificationManager(host)
            {
                MaxItems = 3,
                Position = NotificationPosition.TopRight
            };
            _inAppHost = host;
        }

        _inAppManager?.Show(new Notification(
            $"{GetApplicationName()} · {entry.Level.Description()} · {entry.Timestamp:HH:mm:ss}",
            TrimContent(entry.Message),
            GetNotificationType(entry.Level),
            _duration));
    }

    private void ShowDesktopNotifications(IReadOnlyList<UserLogEntry> entries)
    {
        if (_desktopWindow is null || _desktopWindow.IsClosing)
        {
            var owner = ResolveNotificationOwner();
            if (owner is null) return;

            var window = new NotificationWindow();
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_desktopWindow, window)) _desktopWindow = null;
            };
            _desktopWindow = window;
            ConfigureDesktopWindow();
            window.AddLogs(entries);
            window.Show(owner);
            return;
        }

        ConfigureDesktopWindow();
        _desktopWindow.AddLogs(entries);
    }

    private void ConfigureDesktopWindow()
    {
        _desktopWindow?.Configure(
            GetApplicationName(),
            _duration,
            ResolveNotificationHost(),
            _desktopContentTemplate,
            _attentionMode);
    }

    private void InvalidatePendingNotifications()
    {
        Interlocked.Increment(ref _generation);
        ClearPendingNotifications();
    }

    private void ClearPendingNotifications()
    {
        while (_pendingNotifications.TryDequeue(out _)) Interlocked.Decrement(ref _pendingCount);
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
        {
            return desktop.Windows.LastOrDefault(window =>
                       window.IsActive && !ReferenceEquals(window, _desktopWindow)) ??
                   desktop.MainWindow ??
                   desktop.Windows.LastOrDefault(window =>
                       window.IsVisible && !ReferenceEquals(window, _desktopWindow));
        }

        if (_application.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
            return TopLevel.GetTopLevel(mainView);

        return null;
    }

    private Window? ResolveNotificationOwner()
    {
        if (_application.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        if (desktop.MainWindow is { IsVisible: true } mainWindow)
            return mainWindow;

        return desktop.Windows.LastOrDefault(window =>
            window.IsVisible &&
            window.Owner is null &&
            !ReferenceEquals(window, _desktopWindow));
    }

    private string GetApplicationName()
    {
        if (!string.IsNullOrWhiteSpace(_applicationName)) return _applicationName.Trim();

        using var process = Process.GetCurrentProcess();
        return process.ProcessName;
    }

    private static string TrimContent(string content) =>
        content.Length <= MaxNotificationContentLength
            ? content
            : content[..MaxNotificationContentLength] + "...";

    private static NotificationType GetNotificationType(LogType level)
    {
        return level switch
        {
            LogType.Warn => NotificationType.Warning,
            LogType.Error or LogType.Fatal => NotificationType.Error,
            _ => NotificationType.Information
        };
    }

    private readonly record struct PendingNotification(UserLogEntry Entry, int Generation);
}
