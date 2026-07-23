using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CodeWF.Log.Core;
using CodeWF.Log.Core.Extensions;
using CodeWF.LogViewer.Avalonia.Extensions;
using CodeWF.LogViewer.Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeWF.LogViewer.Avalonia;

/// <summary>
/// 显示用户安全日志的 Avalonia 控件。每个实例可以独立设置显示级别范围。
/// </summary>
public partial class LogView : UserControl
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaximumRefreshInterval = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);

    public static readonly StyledProperty<double> LogLineHeightMultiplierProperty =
        AvaloniaProperty.Register<LogView, double>(nameof(LogLineHeightMultiplier), 1.5);

    public static readonly StyledProperty<LogType> MinimumLevelProperty =
        AvaloniaProperty.Register<LogView, LogType>(nameof(MinimumLevel), LogType.Debug);

    public static readonly StyledProperty<LogType> MaximumLevelProperty =
        AvaloniaProperty.Register<LogView, LogType>(nameof(MaximumLevel), LogType.Fatal);

    public static readonly StyledProperty<int> MaxDisplayCountProperty =
        AvaloniaProperty.Register<LogView, int>(nameof(MaxDisplayCount), 1_000);

    public static readonly StyledProperty<TimeSpan> RefreshIntervalProperty =
        AvaloniaProperty.Register<LogView, TimeSpan>(nameof(RefreshInterval), DefaultRefreshInterval);

    public static readonly StyledProperty<string> TimestampFormatProperty =
        AvaloniaProperty.Register<LogView, string>(nameof(TimestampFormat), "yyyy-MM-dd HH:mm:ss.fff");

    private static readonly SolidColorBrush GrayBrush = new(Color.Parse("#8C8C8C"));
    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#262626"));
    private static readonly SolidColorBrush DebugBrush = new(Color.Parse("#1890FF"));
    private static readonly SolidColorBrush InfoBrush = new(Color.Parse("#52C41A"));
    private static readonly SolidColorBrush WarnBrush = new(Color.Parse("#FAAD14"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#000000"));

    private readonly List<UserLogEntry> _entries = [];
    private IClipboard? _clipboard;
    private ContextMenu _contextMenu = null!;
    private ScrollViewer _scrollViewer = null!;
    private SelectableTextBlock _textView = null!;
    private LogViewSubscription? _subscription;
    private long _clearSequence;
    private long _latestSequence;

    public LogView()
    {
        InitializeComponent();
        InitializeControls();
    }

    public double LogLineHeightMultiplier
    {
        get => GetValue(LogLineHeightMultiplierProperty);
        set => SetValue(LogLineHeightMultiplierProperty, value);
    }

    public LogType MinimumLevel
    {
        get => GetValue(MinimumLevelProperty);
        set => SetValue(MinimumLevelProperty, value);
    }

    public LogType MaximumLevel
    {
        get => GetValue(MaximumLevelProperty);
        set => SetValue(MaximumLevelProperty, value);
    }

    public int MaxDisplayCount
    {
        get => GetValue(MaxDisplayCountProperty);
        set => SetValue(MaxDisplayCountProperty, value);
    }

    public TimeSpan RefreshInterval
    {
        get => GetValue(RefreshIntervalProperty);
        set => SetValue(RefreshIntervalProperty, value);
    }

    public string TimestampFormat
    {
        get => GetValue(TimestampFormatProperty);
        set => SetValue(TimestampFormatProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        StartSubscription();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopSubscription();
        _clipboard = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FontSizeProperty || change.Property == LogLineHeightMultiplierProperty)
        {
            UpdateLogLineHeight();
            return;
        }

        if (change.Property == MinimumLevelProperty ||
            change.Property == MaximumLevelProperty ||
            change.Property == MaxDisplayCountProperty)
        {
            RebuildFromRecentEntries();
            return;
        }

        if (change.Property == RefreshIntervalProperty)
        {
            _subscription?.UpdateRefreshInterval(NormalizeRefreshInterval(RefreshInterval));
            return;
        }

        if (change.Property == TimestampFormatProperty) RenderEntries();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void InitializeControls()
    {
        _textView = this.FindControl<SelectableTextBlock>("LogTextView")
            ?? throw new InvalidOperationException("LogTextView is missing.");
        _scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer")
            ?? throw new InvalidOperationException("LogScrollViewer is missing.");
        _contextMenu = this.FindControl<ContextMenu>("LogContextMenu")
            ?? throw new InvalidOperationException("LogContextMenu is missing.");
        _textView.Text = string.Empty;
        UpdateLogLineHeight();
    }

    private void StartSubscription()
    {
        if (_subscription is not null) return;

        _subscription = new LogViewSubscription(
            Logger.UserLogs,
            ReceiveEntries,
            NormalizeRefreshInterval(RefreshInterval));
    }

    private void StopSubscription()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void ReceiveEntries(IReadOnlyList<UserLogEntry> entries)
    {
        if (entries.Count == 0) return;

        var changed = false;
        foreach (var entry in entries.OrderBy(entry => entry.Sequence))
        {
            _latestSequence = Math.Max(_latestSequence, entry.Sequence);
            if (entry.Sequence <= _clearSequence || !Accepts(entry)) continue;
            if (_entries.Count > 0 && _entries[^1].Sequence >= entry.Sequence) continue;

            _entries.Add(entry);
            changed = true;
        }

        if (!changed) return;

        TrimEntries();
        RenderEntries();
    }

    private void RebuildFromRecentEntries()
    {
        if (_textView is null || _subscription is null) return;

        var recentEntries = Logger.UserLogs.GetRecentEntries();
        _latestSequence = Math.Max(_latestSequence, recentEntries.LastOrDefault()?.Sequence ?? 0);
        _entries.Clear();
        _entries.AddRange(recentEntries.Where(entry => entry.Sequence > _clearSequence && Accepts(entry)));
        TrimEntries();
        RenderEntries();
    }

    private bool Accepts(UserLogEntry entry)
    {
        return MinimumLevel <= MaximumLevel &&
               entry.Level >= MinimumLevel &&
               entry.Level <= MaximumLevel;
    }

    private void TrimEntries()
    {
        var maxCount = Math.Max(1, MaxDisplayCount);
        if (_entries.Count > maxCount) _entries.RemoveRange(0, _entries.Count - maxCount);
    }

    private void RenderEntries()
    {
        var inlines = _textView.Inlines;
        if (inlines is null) return;

        var isAtBottom = _scrollViewer.IsAtVerticalBottom();
        inlines.Clear();
        foreach (var entry in _entries)
        {
            inlines.Add(new Run(entry.Timestamp.ToString(NormalizeTimestampFormat(TimestampFormat)))
            {
                Foreground = GrayBrush,
                BaselineAlignment = BaselineAlignment.Center
            });
            inlines.Add(new Run($"[{entry.Level.Description()}]")
            {
                Foreground = GetLevelForeground(entry.Level),
                FontWeight = entry.Level == LogType.Fatal ? FontWeight.Bold : FontWeight.Normal,
                BaselineAlignment = BaselineAlignment.Center
            });
            inlines.Add(new Run(entry.Message)
            {
                Foreground = TextBrush,
                BaselineAlignment = BaselineAlignment.Center
            });
            inlines.Add(new Run(Environment.NewLine));
        }

        if (isAtBottom) _scrollViewer.ScrollToEnd();
    }

    private void UpdateLogLineHeight()
    {
        if (_textView is null) return;
        if (LogLineHeightMultiplier <= 0 || double.IsNaN(LogLineHeightMultiplier))
        {
            _textView.ClearValue(TextBlock.LineHeightProperty);
            return;
        }

        _textView.LineHeight = Math.Ceiling(FontSize * LogLineHeightMultiplier);
    }

    private static TimeSpan NormalizeRefreshInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) return DefaultRefreshInterval;
        return interval > MaximumRefreshInterval ? MaximumRefreshInterval : interval;
    }

    private static string NormalizeTimestampFormat(string? format) =>
        string.IsNullOrWhiteSpace(format) ? "yyyy-MM-dd HH:mm:ss.fff" : format;

    private static IBrush GetLevelForeground(LogType level)
    {
        return level switch
        {
            LogType.Debug => DebugBrush,
            LogType.Info => InfoBrush,
            LogType.Warn => WarnBrush,
            LogType.Error or LogType.Fatal => ErrorBrush,
            _ => DefaultBrush
        };
    }

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_textView.SelectedText.Length > 0 && _clipboard is not null)
                await _clipboard.SetTextAsync(_textView.SelectedText);
        }
        catch (Exception ex)
        {
            Logger.ErrorToFile("复制日志内容失败。", ex);
        }
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs e)
    {
        _clearSequence = Math.Max(_clearSequence, _latestSequence);
        _entries.Clear();
        _textView.Inlines?.Clear();
    }

    private void Location_OnClick(object? sender, RoutedEventArgs e)
    {
        var logDirectory = Logger.LogDirectory;
        try
        {
            LogFolderLauncher.Open();
        }
        catch (Exception ex)
        {
            Logger.ErrorToFile($"打开日志目录失败：{logDirectory ?? "未启用文件日志"}", ex);
        }
    }

    private void LogScrollViewer_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) _contextMenu.Open();
    }
}
