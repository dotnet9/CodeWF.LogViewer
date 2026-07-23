using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using CodeWF.Log.Core;
using CodeWF.Log.Core.Extensions;
using CodeWF.Log.Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Avalonia.Notifications.ViewModels;

internal sealed class NotificationWindowViewModel : INotifyPropertyChanged
{
    private const int MaxLogCount = 100;
    private const int MaxContentLength = 4000;
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxSessionDuration = TimeSpan.FromMinutes(2);

    private readonly List<LogNotificationContent> _logs = [];
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    private string _applicationName = string.Empty;
    private string _countdownText = string.Empty;
    private string _levelText = string.Empty;
    private string _recordTimeText = string.Empty;
    private string _logContent = string.Empty;
    private string _countText = string.Empty;
    private string _openLogFolderButtonText = "打开日志目录";
    private LogLevel _level = LogLevel.Error;
    private LogNotificationContent? _selectedLog;
    private IDataTemplate? _contentTemplate;
    private TopLevel? _notificationHost;
    private DesktopNotificationAttentionMode _attentionMode = DesktopNotificationAttentionMode.ShakeAndPulse;
    private TimeSpan _duration = DefaultDuration;
    private DateTimeOffset _deadline;
    private DateTimeOffset _sessionDeadline;
    private int _selectedIndex = -1;
    private int _countdownSeconds = -1;
    private double _windowOpacity = 1;
    private bool _canPrevious;
    private bool _canNext;
    private bool _isNavigationVisible;
    private bool _isPointerInside;
    private bool _hasUserNavigated;
    private bool _isOpened;
    private bool _isClosing;
    private bool _hasPendingAttention;
    private LogLevel _pendingAttentionLevel;

    public NotificationWindowViewModel()
    {
        _countdownTimer.Tick += OnCountdownTimerTick;
        PreviousCommand = new NotificationCommand(SelectPrevious);
        NextCommand = new NotificationCommand(SelectNext);
        CloseCommand = new NotificationCommand(RequestClose);
        OpenLogFolderCommand = new NotificationCommand(OpenLogFolder);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<LogLevel>? AttentionRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? PlacementRequested;

    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand OpenLogFolderCommand { get; }

    public string ApplicationName
    {
        get => _applicationName;
        private set => SetField(ref _applicationName, value);
    }

    public string CountdownText
    {
        get => _countdownText;
        private set
        {
            if (!SetField(ref _countdownText, value)) return;
            OnPropertyChanged(nameof(IsCountdownVisible));
        }
    }

    public bool IsCountdownVisible => !string.IsNullOrEmpty(CountdownText);

    public LogLevel Level
    {
        get => _level;
        private set => SetField(ref _level, value);
    }

    public string LevelText
    {
        get => _levelText;
        private set => SetField(ref _levelText, value);
    }

    public string RecordTimeText
    {
        get => _recordTimeText;
        private set => SetField(ref _recordTimeText, value);
    }

    public string LogContent
    {
        get => _logContent;
        private set => SetField(ref _logContent, value);
    }

    public LogNotificationContent? SelectedLog
    {
        get => _selectedLog;
        private set => SetField(ref _selectedLog, value);
    }

    public string CountText
    {
        get => _countText;
        private set => SetField(ref _countText, value);
    }

    public bool CanPrevious
    {
        get => _canPrevious;
        private set => SetField(ref _canPrevious, value);
    }

    public bool CanNext
    {
        get => _canNext;
        private set => SetField(ref _canNext, value);
    }

    public bool IsNavigationVisible
    {
        get => _isNavigationVisible;
        private set => SetField(ref _isNavigationVisible, value);
    }

    public IDataTemplate? ContentTemplate
    {
        get => _contentTemplate;
        private set
        {
            if (!SetField(ref _contentTemplate, value)) return;
            OnPropertyChanged(nameof(IsDefaultContentVisible));
            OnPropertyChanged(nameof(IsCustomContentVisible));
        }
    }

    public bool IsDefaultContentVisible => ContentTemplate == null;
    public bool IsCustomContentVisible => ContentTemplate != null;

    public string OpenLogFolderButtonText
    {
        get => _openLogFolderButtonText;
        private set => SetField(ref _openLogFolderButtonText, value);
    }

    public double WindowOpacity
    {
        get => _windowOpacity;
        private set => SetField(ref _windowOpacity, value);
    }

    public TopLevel? NotificationHost
    {
        get => _notificationHost;
        private set
        {
            if (!SetField(ref _notificationHost, value)) return;
            PlacementRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public DesktopNotificationAttentionMode AttentionMode
    {
        get => _attentionMode;
        private set => SetField(ref _attentionMode, value);
    }

    public bool SuppressShake => _isPointerInside || _hasUserNavigated;
    public bool IsClosing => _isClosing;

    public void Configure(
        string applicationName,
        TimeSpan duration,
        TopLevel? host,
        IDataTemplate? contentTemplate,
        DesktopNotificationAttentionMode attentionMode)
    {
        var normalizedDuration = duration < TimeSpan.Zero ? DefaultDuration : duration;
        var durationChanged = _duration != normalizedDuration;
        _duration = normalizedDuration;
        ApplicationName = applicationName;
        NotificationHost = host;
        ContentTemplate = contentTemplate;
        AttentionMode = attentionMode;
        UpdateSelectedLog();

        if (_isOpened && durationChanged && !_isPointerInside)
            RestartCountdown(resetSessionDeadline: true);
    }

    public void AddLogs(IReadOnlyList<UserLogEntry> logEntries)
    {
        if (logEntries.Count == 0 || _isClosing) return;

        var wasEmpty = _logs.Count == 0;
        var highestLevel = logEntries[0].Level;
        foreach (var logEntry in logEntries)
        {
            _logs.Add(new LogNotificationContent(ApplicationName, logEntry));
            if (logEntry.Level > highestLevel) highestLevel = logEntry.Level;
        }

        TrimLogs();
        if (wasEmpty)
            _selectedIndex = 0;
        else if (_isOpened && !_isPointerInside && !_hasUserNavigated)
            _selectedIndex = _logs.Count - 1;

        WindowOpacity = 1;
        UpdateSelectedLog();
        if (_isOpened && !_isPointerInside) RestartCountdown(resetSessionDeadline: true);
        QueueAttention(highestLevel);
    }

    public void OnOpened()
    {
        _isOpened = true;
        PlacementRequested?.Invoke(this, EventArgs.Empty);
        if (_hasPendingAttention)
        {
            var level = _pendingAttentionLevel;
            _hasPendingAttention = false;
            Dispatcher.UIThread.Post(() => AttentionRequested?.Invoke(level), DispatcherPriority.Loaded);
        }

        StartInitialCountdown();
    }

    public void OnClosed()
    {
        _countdownTimer.Stop();
        _isOpened = false;
        _logs.Clear();
    }

    public void SetPointerInside(bool isInside)
    {
        _isPointerInside = isInside;
        if (isInside)
        {
            _countdownTimer.Stop();
            WindowOpacity = 1;
            UpdateCountdown(GetRemainingTime(), paused: true);
            return;
        }

        _hasUserNavigated = false;
        RestartCountdown(resetSessionDeadline: true);
    }

    public void SelectPrevious() => SelectLog(_selectedIndex - 1);
    public void SelectNext() => SelectLog(_selectedIndex + 1);

    public void RequestClose()
    {
        if (_isClosing) return;

        _isClosing = true;
        _countdownTimer.Stop();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void OpenLogFolder()
    {
        try
        {
            LogFolderLauncher.Open();
            OpenLogFolderButtonText = "打开日志目录";
        }
        catch (Exception ex)
        {
            OpenLogFolderButtonText = "打开失败，请重试";
            System.Diagnostics.Trace.TraceError($"打开日志目录失败：{ex}");
        }
    }

    private void TrimLogs()
    {
        while (_logs.Count > MaxLogCount)
        {
            _logs.RemoveAt(0);
            if (_selectedIndex > 0) _selectedIndex--;
        }
    }

    private void QueueAttention(LogLevel level)
    {
        if (AttentionMode == DesktopNotificationAttentionMode.None || level < LogLevel.Warning) return;

        if (_isOpened)
        {
            AttentionRequested?.Invoke(level);
            return;
        }

        if (!_hasPendingAttention || level > _pendingAttentionLevel) _pendingAttentionLevel = level;
        _hasPendingAttention = true;
    }

    private void StartInitialCountdown()
    {
        WindowOpacity = 1;
        if (_duration == TimeSpan.Zero)
        {
            _countdownTimer.Stop();
            UpdateCountdown(null, paused: false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var logCount = Math.Max(1, _logs.Count);
        var totalTicks = _duration.Ticks >= MaxSessionDuration.Ticks / logCount
            ? MaxSessionDuration.Ticks
            : _duration.Ticks * logCount;
        _sessionDeadline = now + TimeSpan.FromTicks(totalTicks);
        _deadline = now + _duration;
        _countdownTimer.Start();
        UpdateCountdown(_duration, paused: false);
    }

    private void RestartCountdown(bool resetSessionDeadline)
    {
        WindowOpacity = 1;
        if (_duration == TimeSpan.Zero)
        {
            _countdownTimer.Stop();
            UpdateCountdown(null, paused: false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _deadline = now + _duration;
        if (resetSessionDeadline) _sessionDeadline = _deadline;
        _countdownTimer.Start();
        UpdateCountdown(_duration, paused: false);
    }

    private void OnCountdownTimerTick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var remaining = _deadline - now;
        var sessionRemaining = _sessionDeadline - now;
        if (sessionRemaining <= TimeSpan.Zero)
        {
            RequestClose();
            return;
        }

        if (remaining <= TimeSpan.Zero)
        {
            if (!_hasUserNavigated && !_isPointerInside && _selectedIndex < _logs.Count - 1)
            {
                _selectedIndex++;
                UpdateSelectedLog();
                _deadline = now + _duration;
                WindowOpacity = 1;
                return;
            }

            RequestClose();
            return;
        }

        var fadeDuration = GetFadeDuration();
        var effectiveRemaining = remaining < sessionRemaining ? remaining : sessionRemaining;
        var willAdvance = !_hasUserNavigated &&
                          !_isPointerInside &&
                          _selectedIndex < _logs.Count - 1 &&
                          sessionRemaining > remaining;
        WindowOpacity = !willAdvance && effectiveRemaining <= fadeDuration
            ? Math.Clamp(effectiveRemaining.TotalMilliseconds / fadeDuration.TotalMilliseconds, 0, 1)
            : 1;
        UpdateCountdown(remaining, paused: false);
    }

    private TimeSpan GetFadeDuration()
    {
        if (_duration <= TimeSpan.Zero) return TimeSpan.Zero;
        return _duration < TimeSpan.FromSeconds(6)
            ? TimeSpan.FromTicks(Math.Max(1, _duration.Ticks / 2))
            : TimeSpan.FromSeconds(3);
    }

    private void UpdateSelectedLog()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _logs.Count)
        {
            UpdateCountdown(_duration == TimeSpan.Zero ? null : _duration, _isPointerInside);
            return;
        }

        var selectedLog = _logs[_selectedIndex];
        SelectedLog = selectedLog;
        Level = selectedLog.Level;
        LevelText = selectedLog.Level.Description();
        RecordTimeText = selectedLog.RecordTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        LogContent = selectedLog.Content.Length <= MaxContentLength
            ? selectedLog.Content
            : selectedLog.Content[..MaxContentLength] + "...";

        CanPrevious = _selectedIndex > 0;
        CanNext = _selectedIndex < _logs.Count - 1;
        IsNavigationVisible = _logs.Count > 1;
        var newCount = Math.Max(0, _logs.Count - _selectedIndex - 1);
        CountText = newCount > 0
            ? $"{_selectedIndex + 1} / {_logs.Count} · {newCount} 条新日志"
            : $"{_selectedIndex + 1} / {_logs.Count}";
        UpdateCountdown(GetRemainingTime(), _isPointerInside);
    }

    private void UpdateCountdown(TimeSpan? remaining, bool paused)
    {
        var seconds = !paused && remaining.HasValue
            ? Math.Max(1, (int)Math.Ceiling(remaining.Value.TotalSeconds))
            : 0;
        if (_countdownSeconds == seconds) return;

        _countdownSeconds = seconds;
        CountdownText = seconds > 0 ? $"{seconds}s" : string.Empty;
    }

    private TimeSpan? GetRemainingTime()
    {
        if (_duration == TimeSpan.Zero) return null;

        var remaining = _deadline - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : _duration;
    }

    private void SelectLog(int index)
    {
        if (index < 0 || index >= _logs.Count || index == _selectedIndex) return;

        _selectedIndex = index;
        _hasUserNavigated = true;
        WindowOpacity = 1;
        UpdateSelectedLog();
        if (!_isPointerInside) RestartCountdown(resetSessionDeadline: true);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
