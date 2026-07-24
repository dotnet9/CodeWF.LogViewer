using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using CodeWF.Log.Avalonia.Extensions;
using CodeWF.Log.Avalonia.Platform;
using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace CodeWF.Log.Avalonia;

public partial class LogView : UserControl
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaximumRefreshInterval = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);

    public static readonly StyledProperty<LogEventFeed?> SourceProperty =
        AvaloniaProperty.Register<LogView, LogEventFeed?>(nameof(Source));
    public static readonly StyledProperty<double> LogLineHeightMultiplierProperty =
        AvaloniaProperty.Register<LogView, double>(nameof(LogLineHeightMultiplier), 1.5);
    public static readonly StyledProperty<LogLevel> MinimumLevelProperty =
        AvaloniaProperty.Register<LogView, LogLevel>(nameof(MinimumLevel), LogLevel.Information);
    public static readonly StyledProperty<LogLevel> MaximumLevelProperty =
        AvaloniaProperty.Register<LogView, LogLevel>(nameof(MaximumLevel), LogLevel.Critical);
    public static readonly StyledProperty<int> MaxDisplayCountProperty =
        AvaloniaProperty.Register<LogView, int>(nameof(MaxDisplayCount), 1_000);
    public static readonly StyledProperty<TimeSpan> RefreshIntervalProperty =
        AvaloniaProperty.Register<LogView, TimeSpan>(nameof(RefreshInterval), DefaultRefreshInterval);
    public static readonly StyledProperty<string> TimestampFormatProperty =
        AvaloniaProperty.Register<LogView, string>(nameof(TimestampFormat), "yyyy-MM-dd HH:mm:ss.fff");
    public static readonly StyledProperty<string?> LogDirectoryProperty =
        AvaloniaProperty.Register<LogView, string?>(nameof(LogDirectory));

    private static readonly SolidColorBrush DebugBrush = new(Color.Parse("#1890FF"));
    private static readonly SolidColorBrush InfoBrush = new(Color.Parse("#52C41A"));
    private static readonly SolidColorBrush WarnBrush = new(Color.Parse("#FAAD14"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#262626"));

    private readonly List<CodeWFLogEvent> _entries = [];
    private IClipboard? _clipboard;
    private ContextMenu _contextMenu = null!;
    private ScrollViewer _scrollViewer = null!;
    private SelectableTextBlock _textView = null!;
    private LogViewSubscription? _subscription;
    private LogEventFeed? _resolvedSource;
    private long _clearSequence;
    private long _latestSequence;
    private bool _isAttached;

    public LogView()
    {
        AvaloniaXamlLoader.Load(this);
        _textView = this.FindControl<SelectableTextBlock>("LogTextView")!;
        _scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer")!;
        _contextMenu = this.FindControl<ContextMenu>("LogContextMenu")!;
        UpdateLogLineHeight();
    }

    public LogEventFeed? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double LogLineHeightMultiplier { get => GetValue(LogLineHeightMultiplierProperty); set => SetValue(LogLineHeightMultiplierProperty, value); }
    public LogLevel MinimumLevel { get => GetValue(MinimumLevelProperty); set => SetValue(MinimumLevelProperty, value); }
    public LogLevel MaximumLevel { get => GetValue(MaximumLevelProperty); set => SetValue(MaximumLevelProperty, value); }
    public int MaxDisplayCount { get => GetValue(MaxDisplayCountProperty); set => SetValue(MaxDisplayCountProperty, value); }
    public TimeSpan RefreshInterval { get => GetValue(RefreshIntervalProperty); set => SetValue(RefreshIntervalProperty, value); }
    public string TimestampFormat { get => GetValue(TimestampFormatProperty); set => SetValue(TimestampFormatProperty, value); }
    public string? LogDirectory { get => GetValue(LogDirectoryProperty); set => SetValue(LogDirectoryProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        _clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        StartSubscription();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        StopSubscription();
        _clipboard = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty) { StopSubscription(); StartSubscription(); return; }
        if (change.Property == FontSizeProperty || change.Property == LogLineHeightMultiplierProperty) { UpdateLogLineHeight(); return; }
        if (change.Property == MinimumLevelProperty || change.Property == MaximumLevelProperty || change.Property == MaxDisplayCountProperty)
        { RebuildFromRecentEntries(); return; }
        if (change.Property == RefreshIntervalProperty) _subscription?.UpdateRefreshInterval(NormalizeRefreshInterval(RefreshInterval));
        if (change.Property == TimestampFormatProperty) RenderEntries();
    }

    private void StartSubscription()
    {
        if (_subscription is not null || !_isAttached) return;
        _resolvedSource = Source ?? (Application.Current is { } app ? LogContext.GetSource(app) : null) ?? Logger.Events;
        _resolvedSource.LineTemplate.Changed += LineTemplate_OnChanged;
        _subscription = new LogViewSubscription(_resolvedSource, ReceiveEntries, NormalizeRefreshInterval(RefreshInterval));
    }

    private void StopSubscription()
    {
        if (_resolvedSource is not null) _resolvedSource.LineTemplate.Changed -= LineTemplate_OnChanged;
        _resolvedSource = null;
        _subscription?.Dispose();
        _subscription = null;
    }

    private void LineTemplate_OnChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RenderEntries, DispatcherPriority.Background);

    private void ReceiveEntries(IReadOnlyList<CodeWFLogEvent> entries)
    {
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
        if (_resolvedSource is null) return;
        var recent = _resolvedSource.GetRecentEvents();
        _latestSequence = Math.Max(_latestSequence, recent.LastOrDefault()?.Sequence ?? 0);
        _entries.Clear();
        _entries.AddRange(recent.Where(entry => entry.Sequence > _clearSequence && Accepts(entry)));
        TrimEntries();
        RenderEntries();
    }

    private bool Accepts(CodeWFLogEvent entry) =>
        MinimumLevel <= MaximumLevel && entry.Level >= MinimumLevel && entry.Level <= MaximumLevel;

    private void TrimEntries()
    {
        var maxCount = Math.Max(1, MaxDisplayCount);
        if (_entries.Count > maxCount) _entries.RemoveRange(0, _entries.Count - maxCount);
    }

    private void RenderEntries()
    {
        if (_textView.Inlines is not { } inlines) return;
        var isAtBottom = _scrollViewer.IsAtVerticalBottom();
        inlines.Clear();
        var template = _resolvedSource?.LineTemplate.Current ?? LineTemplateController.DefaultTemplate;
        foreach (var entry in _entries)
        {
            inlines.Add(new Run(LogTemplateFormatter.Format(entry, template, NormalizeTimestampFormat(TimestampFormat)))
            {
                Foreground = GetLevelForeground(entry.Level),
                FontWeight = entry.Level == LogLevel.Critical ? FontWeight.Bold : FontWeight.Normal,
                BaselineAlignment = BaselineAlignment.Center
            });
        }
        if (isAtBottom) _scrollViewer.ScrollToEnd();
    }

    private void UpdateLogLineHeight()
    {
        if (LogLineHeightMultiplier <= 0 || double.IsNaN(LogLineHeightMultiplier)) _textView.ClearValue(TextBlock.LineHeightProperty);
        else _textView.LineHeight = Math.Ceiling(FontSize * LogLineHeightMultiplier);
    }

    private static TimeSpan NormalizeRefreshInterval(TimeSpan interval) =>
        interval <= TimeSpan.Zero ? DefaultRefreshInterval : interval > MaximumRefreshInterval ? MaximumRefreshInterval : interval;
    private static string NormalizeTimestampFormat(string? value) => string.IsNullOrWhiteSpace(value) ? "yyyy-MM-dd HH:mm:ss.fff" : value;
    private static IBrush GetLevelForeground(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => DebugBrush,
        LogLevel.Information => InfoBrush,
        LogLevel.Warning => WarnBrush,
        LogLevel.Error or LogLevel.Critical => ErrorBrush,
        _ => DefaultBrush
    };

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        try { if (_textView.SelectedText.Length > 0 && _clipboard is not null) await _clipboard.SetTextAsync(_textView.SelectedText); }
        catch (Exception ex) { Trace.TraceError($"复制日志内容失败：{ex}"); }
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs e)
    {
        _clearSequence = Math.Max(_clearSequence, _latestSequence);
        _entries.Clear();
        _textView.Inlines?.Clear();
    }

    private void Location_OnClick(object? sender, RoutedEventArgs e)
    {
        var directory = LogDirectory ??
                        (Application.Current is { } app ? LogContext.GetLogDirectory(app) : null) ??
                        Logger.LogDirectory;
        try
        {
            if (!LogFolderLauncher.Open(directory))
                Trace.TraceWarning("当前 LogView 未配置日志目录。");
        }
        catch (Exception ex) { Trace.TraceError($"打开日志目录失败（{directory}）：{ex}"); }
    }

    private void LogScrollViewer_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) _contextMenu.Open();
    }
}
