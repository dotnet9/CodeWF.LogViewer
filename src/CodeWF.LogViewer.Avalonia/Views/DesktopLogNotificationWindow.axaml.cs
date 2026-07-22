using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using CodeWF.Log.Core;
using CodeWF.Log.Core.Extensions;
using CodeWF.LogViewer.Avalonia.Behaviors;
using System;
using System.Collections.Generic;

namespace CodeWF.LogViewer.Avalonia.Views;

internal partial class DesktopLogNotificationWindow : Window
{
    private const int MaxLogCount = 100;
    private const int ScreenMargin = 16;
    private const double DefaultWindowWidth = 390;
    private const double FallbackWindowHeight = 430;
    private static readonly string[] LevelClassNames = ["debug", "info", "warn", "error", "fatal"];

    private readonly List<LogNotificationContent> _logs = [];
    private readonly DispatcherTimer _countdownTimer;

    private TextBlock _titleText = null!;
    private TextBlock _countdownText = null!;
    private Border _windowRoot = null!;
    private Border _levelBadge = null!;
    private PathIcon _levelIcon = null!;
    private TextBlock _levelText = null!;
    private TextBlock _recordTimeText = null!;
    private SelectableTextBlock _logContentText = null!;
    private Grid _defaultContent = null!;
    private ContentControl _customContent = null!;
    private Button _previousButton = null!;
    private Button _nextButton = null!;
    private Button _openLogFolderButton = null!;
    private TextBlock _countText = null!;
    private Grid _navigationPanel = null!;
    private DesktopLogNotificationAttentionBehavior _attentionBehavior = null!;

    private string _applicationName = string.Empty;
    private TimeSpan _duration = TimeSpan.FromSeconds(10);
    private DateTimeOffset _deadline;
    private DateTimeOffset _sessionDeadline;
    private TopLevel? _host;
    private int _selectedIndex = -1;
    private bool _isWindowOpened;
    private bool _isPointerInside;
    private bool _hasUserNavigated;
    private bool _isClosing;
    private bool _hasPendingAttention;
    private LogType _pendingAttentionLevel;
    private DesktopNotificationAttentionMode _attentionMode = DesktopNotificationAttentionMode.ShakeAndPulse;

    public DesktopLogNotificationWindow()
    {
        InitializeComponent();
        ResolveControls();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _countdownTimer.Tick += CountdownTimer_OnTick;
        Opened += DesktopLogNotificationWindow_OnOpened;
        Closed += DesktopLogNotificationWindow_OnClosed;
        SizeChanged += DesktopLogNotificationWindow_OnSizeChanged;
    }

    public bool IsClosing => _isClosing;

    public void Configure(
        string applicationName,
        TimeSpan duration,
        TopLevel? host,
        IDataTemplate? contentTemplate,
        DesktopNotificationAttentionMode attentionMode)
    {
        var normalizedDuration = duration < TimeSpan.Zero ? TimeSpan.FromSeconds(10) : duration;
        var durationChanged = _duration != normalizedDuration;
        _applicationName = applicationName;
        _duration = normalizedDuration;
        _host = host;
        if (_attentionMode != attentionMode)
        {
            _attentionMode = attentionMode;
            _attentionBehavior.Mode = attentionMode;
        }
        _customContent.ContentTemplate = contentTemplate;
        _customContent.IsVisible = contentTemplate != null;
        _defaultContent.IsVisible = contentTemplate == null;
        UpdateSelectedLog();
        if (IsVisible)
        {
            Dispatcher.UIThread.Post(PositionAtBottomRight, DispatcherPriority.Loaded);
            if (durationChanged && !_isPointerInside)
            {
                RestartCountdown(resetSessionDeadline: true);
            }
        }
    }

    public void AddLogs(IReadOnlyList<LogInfo> logInfos)
    {
        if (logInfos.Count == 0 || _isClosing)
        {
            return;
        }

        var wasEmpty = _logs.Count == 0;
        var highestLevel = logInfos[0].Level;
        foreach (var logInfo in logInfos)
        {
            _logs.Add(new LogNotificationContent(_applicationName, logInfo));
            if (logInfo.Level > highestLevel)
            {
                highestLevel = logInfo.Level;
            }
        }

        while (_logs.Count > MaxLogCount)
        {
            _logs.RemoveAt(0);
            if (_selectedIndex > 0)
            {
                _selectedIndex--;
            }
        }

        if (wasEmpty)
        {
            _selectedIndex = 0;
        }
        else if (_isWindowOpened && !_isPointerInside && !_hasUserNavigated)
        {
            _selectedIndex = _logs.Count - 1;
        }

        Opacity = 1;
        UpdateSelectedLog();
        if (_isWindowOpened && !_isPointerInside)
        {
            RestartCountdown(resetSessionDeadline: true);
        }

        QueueAttention(highestLevel);
    }

    public void CloseNotification()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _countdownTimer.Stop();
        Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ResolveControls()
    {
        _windowRoot = FindRequired<Border>("WindowRoot");
        _titleText = FindRequired<TextBlock>("TitleText");
        _countdownText = FindRequired<TextBlock>("CountdownText");
        _levelBadge = FindRequired<Border>("LevelBadge");
        _levelIcon = FindRequired<PathIcon>("LevelIcon");
        _levelText = FindRequired<TextBlock>("LevelText");
        _recordTimeText = FindRequired<TextBlock>("RecordTimeText");
        _logContentText = FindRequired<SelectableTextBlock>("LogContentText");
        _defaultContent = FindRequired<Grid>("DefaultContent");
        _customContent = FindRequired<ContentControl>("CustomContent");
        _previousButton = FindRequired<Button>("PreviousButton");
        _nextButton = FindRequired<Button>("NextButton");
        _openLogFolderButton = FindRequired<Button>("OpenLogFolderButton");
        _countText = FindRequired<TextBlock>("CountText");
        _navigationPanel = FindRequired<Grid>("NavigationPanel");
        _attentionBehavior = FindRequiredBehavior<DesktopLogNotificationAttentionBehavior>(_windowRoot);
        _attentionBehavior.PulseTarget = _levelBadge;
    }

    private T FindRequired<T>(string name) where T : Control
    {
        return this.FindControl<T>(name) ?? throw new InvalidOperationException($"{name} is missing.");
    }

    private static T FindRequiredBehavior<T>(AvaloniaObject associatedObject) where T : Behavior
    {
        foreach (var behavior in Interaction.GetBehaviors(associatedObject))
        {
            if (behavior is T expectedBehavior) return expectedBehavior;
        }

        throw new InvalidOperationException($"{typeof(T).Name} is missing.");
    }

    private void DesktopLogNotificationWindow_OnOpened(object? sender, EventArgs e)
    {
        _isWindowOpened = true;
        Screens.Changed += Screens_OnChanged;
        Dispatcher.UIThread.Post(PositionAtBottomRight, DispatcherPriority.Loaded);
        if (_hasPendingAttention)
        {
            var level = _pendingAttentionLevel;
            _hasPendingAttention = false;
            Dispatcher.UIThread.Post(
                () => _attentionBehavior.Play(level, _isPointerInside || _hasUserNavigated),
                DispatcherPriority.Loaded);
        }
        StartInitialCountdown();
    }

    private void DesktopLogNotificationWindow_OnClosed(object? sender, EventArgs e)
    {
        _countdownTimer.Stop();
        Screens.Changed -= Screens_OnChanged;
        _attentionBehavior.Stop();
        _isWindowOpened = false;
        _logs.Clear();
    }

    private void DesktopLogNotificationWindow_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isWindowOpened)
        {
            Dispatcher.UIThread.Post(PositionAtBottomRight, DispatcherPriority.Loaded);
        }
    }

    private void Screens_OnChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(PositionAtBottomRight, DispatcherPriority.Loaded);
    }

    private void QueueAttention(LogType level)
    {
        if (_attentionMode == DesktopNotificationAttentionMode.None || level < LogType.Warn)
        {
            return;
        }

        if (!_isWindowOpened)
        {
            if (!_hasPendingAttention || level > _pendingAttentionLevel)
            {
                _pendingAttentionLevel = level;
            }

            _hasPendingAttention = true;
            return;
        }

        _attentionBehavior.Play(level, _isPointerInside || _hasUserNavigated);
    }

    private void PositionAtBottomRight()
    {
        var screen = GetTargetScreen();
        if (screen == null)
        {
            return;
        }

        var scaling = screen.Scaling;
        var width = GetPixelSize(Bounds.Width, Width, DefaultWindowWidth, scaling);
        var height = GetPixelSize(Bounds.Height, Height, FallbackWindowHeight, scaling);
        var margin = (int)Math.Ceiling(ScreenMargin * scaling);
        var workingArea = screen.WorkingArea;

        Position = new PixelPoint(
            workingArea.Right - width - margin,
            workingArea.Bottom - height - margin);
    }

    private static int GetPixelSize(double renderedSize, double configuredSize, double defaultSize, double scaling)
    {
        var size = IsValidSize(renderedSize)
            ? renderedSize
            : IsValidSize(configuredSize)
                ? configuredSize
                : defaultSize;
        return Math.Max(1, (int)Math.Ceiling(size * scaling));
    }

    private static bool IsValidSize(double value)
    {
        return double.IsFinite(value) && value > 0;
    }

    private global::Avalonia.Platform.Screen? GetTargetScreen()
    {
        try
        {
            if (_host is Window hostWindow)
            {
                var center = new PixelPoint(
                    hostWindow.Position.X +
                    (int)Math.Ceiling(hostWindow.Bounds.Width * hostWindow.RenderScaling / 2),
                    hostWindow.Position.Y +
                    (int)Math.Ceiling(hostWindow.Bounds.Height * hostWindow.RenderScaling / 2));
                return Screens.ScreenFromPoint(center) ?? Screens.Primary;
            }

            if (_host != null)
            {
                return Screens.ScreenFromTopLevel(_host) ?? Screens.Primary;
            }
        }
        catch (ObjectDisposedException)
        {
        }

        return Screens.Primary;
    }

    private void StartInitialCountdown()
    {
        Opacity = 1;
        if (_duration == TimeSpan.Zero)
        {
            _countdownTimer.Stop();
            UpdateTitle(null, false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var maxSessionTicks = TimeSpan.FromMinutes(2).Ticks;
        var logCount = Math.Max(1, _logs.Count);
        var totalTicks = _duration.Ticks >= maxSessionTicks / logCount
            ? maxSessionTicks
            : _duration.Ticks * logCount;
        _sessionDeadline = now + TimeSpan.FromTicks(totalTicks);
        _deadline = now + _duration;
        _countdownTimer.Start();
        UpdateTitle(_duration, false);
    }

    private void RestartCountdown(bool resetSessionDeadline)
    {
        Opacity = 1;
        if (_duration == TimeSpan.Zero)
        {
            _countdownTimer.Stop();
            UpdateTitle(null, false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _deadline = now + _duration;
        if (resetSessionDeadline)
        {
            _sessionDeadline = _deadline;
        }
        _countdownTimer.Start();
        UpdateTitle(_duration, false);
    }

    private void CountdownTimer_OnTick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var remaining = _deadline - now;
        var sessionRemaining = _sessionDeadline - now;
        if (sessionRemaining <= TimeSpan.Zero)
        {
            CloseNotification();
            return;
        }

        if (remaining <= TimeSpan.Zero)
        {
            if (!_hasUserNavigated && !_isPointerInside && _selectedIndex < _logs.Count - 1)
            {
                _selectedIndex++;
                UpdateSelectedLog();
                _deadline = now + _duration;
                Opacity = 1;
                return;
            }

            CloseNotification();
            return;
        }

        var fadeDuration = GetFadeDuration();
        var effectiveRemaining = remaining < sessionRemaining ? remaining : sessionRemaining;
        var willAdvance = !_hasUserNavigated &&
                          !_isPointerInside &&
                          _selectedIndex < _logs.Count - 1 &&
                          sessionRemaining > remaining;
        Opacity = !willAdvance && effectiveRemaining <= fadeDuration
            ? Math.Clamp(effectiveRemaining.TotalMilliseconds / fadeDuration.TotalMilliseconds, 0, 1)
            : 1;
        UpdateTitle(remaining, false);
    }

    private TimeSpan GetFadeDuration()
    {
        if (_duration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (_duration < TimeSpan.FromSeconds(6))
        {
            return TimeSpan.FromTicks(Math.Max(1, _duration.Ticks / 2));
        }

        return TimeSpan.FromSeconds(3);
    }

    private void UpdateSelectedLog()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _logs.Count)
        {
            UpdateTitle(_duration == TimeSpan.Zero ? null : _duration, _isPointerInside);
            return;
        }

        var selectedLog = _logs[_selectedIndex];
        _levelText.Text = selectedLog.Level.Description();
        _recordTimeText.Text = selectedLog.RecordTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        _logContentText.Text = TrimContent(selectedLog.Content);
        _customContent.Content = selectedLog;

        UpdateLevelClasses(selectedLog.Level);

        _previousButton.IsEnabled = _selectedIndex > 0;
        _nextButton.IsEnabled = _selectedIndex < _logs.Count - 1;
        _navigationPanel.IsVisible = _logs.Count > 1;

        var newCount = Math.Max(0, _logs.Count - _selectedIndex - 1);
        _countText.Text = newCount > 0
            ? $"{_selectedIndex + 1} / {_logs.Count} · {newCount} 条新日志"
            : $"{_selectedIndex + 1} / {_logs.Count}";
        UpdateTitle(GetRemainingTime(), _isPointerInside);
    }

    private void UpdateTitle(TimeSpan? remaining, bool paused)
    {
        _titleText.Text = _applicationName;
        _countdownText.Text = !paused && remaining.HasValue
            ? $"{Math.Max(1, (int)Math.Ceiling(remaining.Value.TotalSeconds))}s"
            : string.Empty;
        _countdownText.IsVisible = !string.IsNullOrEmpty(_countdownText.Text);
        Title = _applicationName;
    }

    private TimeSpan? GetRemainingTime()
    {
        if (_duration == TimeSpan.Zero)
        {
            return null;
        }

        var remaining = _deadline - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : _duration;
    }

    private void SelectLog(int index)
    {
        if (index < 0 || index >= _logs.Count || index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        _hasUserNavigated = true;
        Opacity = 1;
        UpdateSelectedLog();
        if (!_isPointerInside)
        {
            RestartCountdown(resetSessionDeadline: true);
        }
    }

    private void UpdateLevelClasses(LogType level)
    {
        var levelClass = level switch
        {
            LogType.Debug => "debug",
            LogType.Info => "info",
            LogType.Warn => "warn",
            LogType.Fatal => "fatal",
            _ => "error"
        };

        foreach (var name in LevelClassNames)
        {
            _levelBadge.Classes.Set(name, name == levelClass);
            _levelIcon.Classes.Set(name, name == levelClass);
            _levelText.Classes.Set(name, name == levelClass);
        }
    }

    private static string TrimContent(string content)
    {
        const int maxLength = 4000;
        return content.Length <= maxLength ? content : content[..maxLength] + "...";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Left:
                SelectLog(_selectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Right:
                SelectLog(_selectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Escape:
                CloseNotification();
                e.Handled = true;
                break;
            case Key.O when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                OpenLogFolder();
                e.Handled = true;
                break;
        }
    }

    private void WindowRoot_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isPointerInside = true;
        _countdownTimer.Stop();
        Opacity = 1;
        UpdateTitle(GetRemainingTime(), true);
    }

    private void WindowRoot_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isPointerInside = false;
        _hasUserNavigated = false;
        RestartCountdown(resetSessionDeadline: true);
    }

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectLog(_selectedIndex - 1);
    }

    private void NextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectLog(_selectedIndex + 1);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseNotification();
    }

    private void OpenLogFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenLogFolder();
    }

    private void OpenLogFolder()
    {
        try
        {
            LogFolderLauncher.Open();
            _openLogFolderButton.Content = "打开日志目录";
            ToolTip.SetTip(_openLogFolderButton, "打开本地日志目录（Ctrl+O）");
        }
        catch (Exception ex)
        {
            _openLogFolderButton.Content = "打开失败，请重试";
            ToolTip.SetTip(_openLogFolderButton, $"打开日志目录失败：{ex.Message}");
        }
    }

    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseNotification();
    }
}
