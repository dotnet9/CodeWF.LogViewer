using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using CodeWF.Log.Core;
using CodeWF.Log.Core.Extensions;
using CodeWF.LogViewer.Avalonia.Extensions;
using CodeWF.Tools.FileExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWF.LogViewer.Avalonia;

public partial class LogView : UserControl
{
    private static readonly TimeSpan DefaultNotificationDuration = TimeSpan.FromSeconds(5);

    public static readonly StyledProperty<double> LogLineHeightMultiplierProperty =
        AvaloniaProperty.Register<LogView, double>(nameof(LogLineHeightMultiplier), 1.5);

    public static readonly StyledProperty<bool> IsNotificationEnabledProperty =
        AvaloniaProperty.Register<LogView, bool>(nameof(IsNotificationEnabled), false);

    public static readonly StyledProperty<LogType> NotificationLevelProperty =
        AvaloniaProperty.Register<LogView, LogType>(nameof(NotificationLevel), LogType.Error);

    public static readonly StyledProperty<TimeSpan> NotificationDurationProperty =
        AvaloniaProperty.Register<LogView, TimeSpan>(nameof(NotificationDuration), DefaultNotificationDuration);

    public static readonly StyledProperty<TopLevel?> NotificationHostProperty =
        AvaloniaProperty.Register<LogView, TopLevel?>(nameof(NotificationHost));

    private IClipboard? _clipboard;
    private ContextMenu _contextMenu = null!;

    private bool _isRecording;
    private ScrollViewer _scrollViewer = null!;
    private SelectableTextBlock _textView = null!;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private WindowNotificationManager? _notificationManager;
    private TopLevel? _notificationManagerHost;
    private bool _isAttachedToVisualTree;
    private bool _isNotificationSubscribed;
    private int _notificationEnabled;
    private int _notificationLevel = (int)LogType.Error;

    // 修复：使用Brush对象池，避免重复创建
    private static readonly SolidColorBrush GrayBrush = new(Color.Parse("#8C8C8C"));

    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#262626"));
    private static readonly SolidColorBrush DebugBrush = new(Color.Parse("#1890FF"));
    private static readonly SolidColorBrush InfoBrush = new(Color.Parse("#52C41A"));
    private static readonly SolidColorBrush WarnBrush = new(Color.Parse("#FAAD14"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush FatalBrush = new(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#000000"));

    public double LogLineHeightMultiplier
    {
        get => GetValue(LogLineHeightMultiplierProperty);
        set => SetValue(LogLineHeightMultiplierProperty, value);
    }

    /// <summary>
    /// 是否弹出重要日志通知。默认关闭，避免升级组件后改变现有应用行为。
    /// </summary>
    public bool IsNotificationEnabled
    {
        get => GetValue(IsNotificationEnabledProperty);
        set => SetValue(IsNotificationEnabledProperty, value);
    }

    /// <summary>
    /// 弹出通知的最低日志级别。默认 <see cref="LogType.Error"/>。
    /// </summary>
    public LogType NotificationLevel
    {
        get => GetValue(NotificationLevelProperty);
        set => SetValue(NotificationLevelProperty, value);
    }

    /// <summary>
    /// 通知自动关闭时间。默认 5 秒，设置为 <see cref="TimeSpan.Zero"/> 时不自动关闭。
    /// </summary>
    public TimeSpan NotificationDuration
    {
        get => GetValue(NotificationDurationProperty);
        set => SetValue(NotificationDurationProperty, value);
    }

    /// <summary>
    /// 通知显示宿主。未设置时自动使用当前控件所属的 <see cref="TopLevel"/>。
    /// </summary>
    public TopLevel? NotificationHost
    {
        get => GetValue(NotificationHostProperty);
        set => SetValue(NotificationHostProperty, value);
    }

    public LogView()
    {
        InitializeComponent();
        _cancellationTokenSource = new CancellationTokenSource();
        Init();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttachedToVisualTree = false;
        UnsubscribeFromLogNotifications();
        _notificationManager?.CloseAll();
        _notificationManager = null;
        _notificationManagerHost = null;
        // 清理资源，停止后台任务
        _cancellationTokenSource?.Cancel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttachedToVisualTree = true;
        var level = TopLevel.GetTopLevel(this);
        if (level != null)
        {
            _clipboard = level.Clipboard;
        }
        EnsureNotificationManager();
        SubscribeToLogNotifications();
    }

    private void Init()
    {
        _textView = this.FindControl<SelectableTextBlock>("LogTextView")
            ?? throw new InvalidOperationException("LogTextView is missing.");
        _scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer")
            ?? throw new InvalidOperationException("LogScrollViewer is missing.");
        _contextMenu = this.FindControl<ContextMenu>("LogContextMenu")
            ?? throw new InvalidOperationException("LogContextMenu is missing.");
        _textView.Text = string.Empty;
        UpdateLogLineHeight();
        RecordLog();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FontSizeProperty || change.Property == LogLineHeightMultiplierProperty)
        {
            UpdateLogLineHeight();
        }

        else if (change.Property == IsNotificationEnabledProperty)
        {
            Volatile.Write(ref _notificationEnabled, change.GetNewValue<bool>() ? 1 : 0);
            if (!change.GetNewValue<bool>())
            {
                _notificationManager?.CloseAll();
            }
        }
        else if (change.Property == NotificationLevelProperty)
        {
            Volatile.Write(ref _notificationLevel, (int)change.GetNewValue<LogType>());
        }
        else if (change.Property == NotificationHostProperty && _isAttachedToVisualTree)
        {
            EnsureNotificationManager();
        }
    }

    private void SubscribeToLogNotifications()
    {
        if (_isNotificationSubscribed)
        {
            return;
        }

        Logger.LogPublished += Logger_LogPublished;
        _isNotificationSubscribed = true;
    }

    private void UnsubscribeFromLogNotifications()
    {
        if (!_isNotificationSubscribed)
        {
            return;
        }

        Logger.LogPublished -= Logger_LogPublished;
        _isNotificationSubscribed = false;
    }

    private void Logger_LogPublished(LogInfo logInfo)
    {
        if (Volatile.Read(ref _notificationEnabled) == 0 ||
            (int)logInfo.Level < Volatile.Read(ref _notificationLevel))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => ShowLogNotification(logInfo));
    }

    private void ShowLogNotification(LogInfo logInfo)
    {
        if (!IsNotificationEnabled || logInfo.Level < NotificationLevel || !_isAttachedToVisualTree)
        {
            return;
        }

        EnsureNotificationManager();
        if (_notificationManager == null)
        {
            return;
        }

        var duration = NotificationDuration < TimeSpan.Zero
            ? DefaultNotificationDuration
            : NotificationDuration;
        var title = $"{logInfo.Level.Description()} · {logInfo.RecordTime:HH:mm:ss}";
        var content = string.IsNullOrWhiteSpace(logInfo.FriendlyDescription)
            ? logInfo.Description
            : logInfo.FriendlyDescription;

        _notificationManager.Show(new Notification(
            title,
            TrimNotificationContent(content),
            GetNotificationType(logInfo.Level),
            duration));
    }

    private void EnsureNotificationManager()
    {
        var host = NotificationHost ?? TopLevel.GetTopLevel(this);
        if (host == null || ReferenceEquals(host, _notificationManagerHost))
        {
            return;
        }

        _notificationManager?.CloseAll();
        _notificationManager = new WindowNotificationManager(host)
        {
            MaxItems = 3,
            Position = NotificationPosition.TopRight
        };
        _notificationManagerHost = host;
    }

    private static NotificationType GetNotificationType(LogType level)
    {
        return level switch
        {
            LogType.Warn => NotificationType.Warning,
            LogType.Error or LogType.Fatal => NotificationType.Error,
            _ => NotificationType.Information
        };
    }

    private static string TrimNotificationContent(string content)
    {
        const int maxLength = 500;
        return content.Length <= maxLength ? content : content[..maxLength] + "...";
    }

    private void UpdateLogLineHeight()
    {
        if (_textView is null)
        {
            return;
        }

        if (LogLineHeightMultiplier <= 0 || double.IsNaN(LogLineHeightMultiplier))
        {
            _textView.ClearValue(TextBlock.LineHeightProperty);
            return;
        }

        _textView.LineHeight = Math.Ceiling(FontSize * LogLineHeightMultiplier);
    }

    private void RecordLog()
    {
        if (_isRecording) return;

        _isRecording = true;
        Logger.RecordToFile();

        Task.Run(async () =>
        {
            var logsBatch = new List<LogInfo>();
            var batchLock = new object();
            var debounceTask = Task.CompletedTask;

            try
            {
                await foreach (var log in Logger.ReadAllUiLogsAsync(_cancellationTokenSource.Token))
                {
                    List<LogInfo>? batchToRender = null;

                    lock (batchLock)
                    {
                        logsBatch.Add(log);

                        if (logsBatch.Count >= Logger.BatchProcessSize)
                        {
                            batchToRender = new List<LogInfo>(logsBatch);
                            logsBatch.Clear();
                        }
                    }

                    if (batchToRender != null)
                    {
                        await RenderLogBatchAsync(batchToRender);
                    }
                    else if (debounceTask.IsCompleted)
                    {
                        debounceTask = DebounceUpdateUiAsync(logsBatch, batchLock);
                    }
                }

                await RenderPendingBatchAsync(logsBatch, batchLock);
            }
            catch (OperationCanceledException)
            {
                await RenderPendingBatchAsync(logsBatch, batchLock);
            }
        });
    }

    private async Task DebounceUpdateUiAsync(List<LogInfo> logsBatch, object batchLock)
    {
        try
        {
            await Task.Delay((int)Logger.LogUIDuration, _cancellationTokenSource.Token);
            await RenderPendingBatchAsync(logsBatch, batchLock);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private async Task RenderPendingBatchAsync(List<LogInfo> logsBatch, object batchLock)
    {
        var batchToRender = TakePendingBatch(logsBatch, batchLock);
        if (batchToRender != null)
        {
            await RenderLogBatchAsync(batchToRender);
        }
    }

    private static List<LogInfo>? TakePendingBatch(List<LogInfo> logsBatch, object batchLock)
    {
        lock (batchLock)
        {
            if (logsBatch.Count == 0)
            {
                return null;
            }

            var batchToRender = new List<LogInfo>(logsBatch);
            logsBatch.Clear();
            return batchToRender;
        }
    }

    private async Task RenderLogBatchAsync(IReadOnlyList<LogInfo> logsBatch)
    {
        await Dispatcher.UIThread.InvokeAsync(() => UpdateLogUi(logsBatch));
    }

    /// <summary>
    /// 批量更新日志UI
    /// </summary>
    /// <param name="logsBatch">日志批次</param>
    private void UpdateLogUi(IReadOnlyList<LogInfo>? logsBatch)
    {
        if (logsBatch == null || logsBatch.Count == 0)
            return;

        var inlines = _textView.Inlines;
        if (inlines == null) return;

        try
        {
            if (inlines.Count > Logger.MaxUIDisplayCount)
            {
                var removeCount =
                    Math.Min(inlines.Count - Logger.MaxUIDisplayCount + logsBatch.Count * 4, inlines.Count / 2);

                for (var i = 0; i < removeCount; i++)
                {
                    if (inlines.Count > 0)
                    {
                        if (inlines[0] is Run run)
                        {
                            run.Foreground = null;
                            run.Text = null;
                        }

                        inlines.RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // 批量添加日志到UI
            var runs = new List<Inline>();
            foreach (var logInfo in logsBatch)
            {
                runs.Add(new Run($"{logInfo.RecordTime.ToString(Logger.TimeFormat)}")
                {
                    Foreground = GrayBrush,
                    BaselineAlignment = BaselineAlignment.Center
                });
                var levelRun = new Run($"[{logInfo.Level.Description()}]") // 修复中文乱码，使用方括号替代
                {
                    Foreground = GetLevelForeground(logInfo.Level),
                };
                if (logInfo.Level == LogType.Fatal)
                {
                    levelRun.FontWeight = FontWeight.Bold;
                }

                runs.Add(levelRun);
                var logMessage = string.IsNullOrWhiteSpace(logInfo.FriendlyDescription)
                    ? logInfo.Description
                    : logInfo.FriendlyDescription;
                runs.Add(new Run(logMessage)
                {
                    Foreground = TextBrush,
                    BaselineAlignment = BaselineAlignment.Center
                });
                runs.Add(new Run(Environment.NewLine));
            }

            var isAtBottom = _scrollViewer.IsAtVerticalBottom();
            inlines.AddRange(runs);
            if (isAtBottom)
            {
                _scrollViewer.ScrollToEnd();
            }
        }
        catch
        {
            // ignored
        }
    }

    private IBrush GetLevelForeground(LogType level)
    {
        return level switch
        {
            LogType.Debug => DebugBrush,
            LogType.Info => InfoBrush,
            LogType.Warn => WarnBrush,
            LogType.Error => ErrorBrush,
            LogType.Fatal => FatalBrush,
            _ => DefaultBrush
        };
    }

    private async void Copy_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_textView.SelectedText.Length > 0 && _clipboard != null)
                await _clipboard.SetTextAsync(_textView.SelectedText);
        }
        catch
        {
            // ignored
        }
    }

    private void Clear_OnClick(object sender, RoutedEventArgs e)
    {
        _textView.Inlines?.Clear();
    }

    private void Location_OnClick(object sender, RoutedEventArgs e)
    {
        var logFolder = Path.Combine(Logger.LogDir, "Log");

        try
        {
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }
            FileHelper.OpenFolder(logFolder);
        }
        catch (Exception ex)
        {
            Logger.Error($"Open log dir exception, the dir is {logFolder}", ex);
        }
    }

    private void LogScrollViewer_OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) _contextMenu.Open();
    }
}
