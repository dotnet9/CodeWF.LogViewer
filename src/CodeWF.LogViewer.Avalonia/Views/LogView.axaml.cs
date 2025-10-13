using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using CodeWF.LogViewer.Avalonia.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWF.LogViewer.Avalonia;

public partial class LogView : UserControl
{
    private const int MaxCount = 1000;
    private readonly SynchronizationContext _synchronizationContext;
    private IClipboard _clipboard;
    private ContextMenu _contextMenu;

    private bool _isRecording;
    private ScrollViewer _scrollViewer;
    private SelectableTextBlock _textView;
    private CancellationTokenSource _cancellationTokenSource;

    // 修复：使用Brush对象池，避免重复创建
    private static readonly SolidColorBrush _grayBrush = new SolidColorBrush(Color.Parse("#8C8C8C"));

    private static readonly SolidColorBrush _textBrush = new SolidColorBrush(Color.Parse("#262626"));
    private static readonly SolidColorBrush _debugBrush = new SolidColorBrush(Color.Parse("#1890FF"));
    private static readonly SolidColorBrush _infoBrush = new SolidColorBrush(Color.Parse("#52C41A"));
    private static readonly SolidColorBrush _warnBrush = new SolidColorBrush(Color.Parse("#FAAD14"));
    private static readonly SolidColorBrush _errorBrush = new SolidColorBrush(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush _fatalBrush = new SolidColorBrush(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush _defaultBrush = new SolidColorBrush(Color.Parse("#000000"));

    public LogView()
    {
        InitializeComponent();
        _synchronizationContext = SynchronizationContext.Current;
        _cancellationTokenSource = new CancellationTokenSource();
        Init();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
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
        var level = TopLevel.GetTopLevel(this);
        if (level == null) return;

        _clipboard = level.Clipboard;
    }

    private void Init()
    {
        _textView = this.Find<SelectableTextBlock>("LogTextView");
        _scrollViewer = this.Find<ScrollViewer>("LogScrollViewer");
        _contextMenu = this.Find<ContextMenu>("LogContextMenu");
        _textView.Text = string.Empty;
        RecordLog();
    }

    private void RecordLog()
    {
        if (_isRecording) return;

        _isRecording = true;

        Task.Run(async () =>
        {
            var logsBatch = new List<LogInfo>();
            var token = _cancellationTokenSource.Token;

            // 用于节流UI更新的计数器
            int uiUpdateThrottleCounter = 0;
            const int UiUpdateThrottle = 3; // 每3批只更新一次UI

            try
            {
                while (!token.IsCancellationRequested)
                {
                    logsBatch.Clear();

                    // 批量收集日志 - 增加批次大小，特别是高负载时
                    int batchSize = Logger.Logs.Count > 500 ? 300 : (Logger.Logs.Count > 200 ? 200 : Logger.BatchProcessSize);
                    int count = 0;
                    while (count < batchSize && Logger.TryDequeue(out var log))
                    {
                        if (Logger.Level <= log.Level)
                        {
                            logsBatch.Add(log);
                        }
                        count++;
                    }

                    if (logsBatch.Count > 0)
                    {
                        // 立即写入文件，避免阻塞UI线程
                        Task.Run(() =>
                        {
                            try
                            {
                                Logger.AddLogBatchToFile(logsBatch);
                            }
                            catch { /* ignored */ }
                        }, token);

                        // UI更新节流 - 减少UI更新频率
                        uiUpdateThrottleCounter++;
                        if (uiUpdateThrottleCounter >= UiUpdateThrottle || Logger.Logs.Count == 0)
                        {
                            uiUpdateThrottleCounter = 0;
                            _synchronizationContext.Post(o =>
                            {
                                try
                                {
                                    UpdateLogUI((System.Collections.Generic.List<LogInfo>)o);
                                }
                                catch { /* ignored */ }
                            }, logsBatch.ToList()); // 传递副本避免并发问题
                        }
                    }

                    // 智能休眠策略
                    int queueSize = Logger.Logs.Count;
                    int delayMs;
                    if (queueSize > 500) delayMs = 5;      // 大量日志时最小延迟
                    else if (queueSize > 100) delayMs = 10; // 中等量日志
                    else if (queueSize > 0) delayMs = 20;   // 少量日志
                    else delayMs = 100;                     // 无日志时

                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token);
                }
            }
            catch (OperationCanceledException) { /* 任务被取消，正常退出 */ }
        });
    }

    private void LogNotifyHandler(LogInfo logInfo)
    {
        // 这个方法现在仅作为兼容保留，实际处理已移至批量处理方法
        if (Logger.Level > logInfo.Level) return;

        _synchronizationContext.Post(o =>
        {
            try
            {
                UpdateLogUI(new System.Collections.Generic.List<LogInfo> { (LogInfo)o });
            }
            catch { /* ignored */ }
        }, logInfo);
    }

    /// <summary>
    /// 批量更新日志UI
    /// </summary>
    /// <param name="logsBatch">日志批次</param>
    private void UpdateLogUI(System.Collections.Generic.List<LogInfo> logsBatch)
    {
        if (logsBatch == null || logsBatch.Count == 0)
            return;

        // 减少UI更新频率 - 如果当前批次很小且队列还有大量日志，则跳过更新
        if (logsBatch.Count < 50 && Logger.Logs.Count > 100) return;

        var inlines = _textView.Inlines;
        if (inlines == null) return;

        try
        {
            // 批量清理超出限制的日志 - 优化版本：批量删除而不是逐条删除
            if (inlines.Count > MaxCount)
            {
                int removeCount = Math.Min(inlines.Count - MaxCount + logsBatch.Count * 4, inlines.Count / 2); // 一次性删除更多日志
                // 重要优化：创建新的Inlines集合代替逐条删除
                //var newInlines = new System.Collections.Generic.List<Inline>();
                //for (int i = removeCount; i < inlines.Count; i++)
                //{
                //    newInlines.Add(inlines[i]);
                //}
                //inlines.Clear();
                //foreach (var inline in newInlines)
                //{
                //    inlines.Add(inline);
                //}

                //修复：释放资源，避免内存泄漏
                for (int i = 0; i < removeCount; i++)
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
            var runs = new System.Collections.Generic.List<Inline>();
            foreach (var logInfo in logsBatch)
            {
                runs.Add(new Run($"{logInfo.RecordTime}")
                {
                    Foreground = _grayBrush,
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
                runs.Add(new Run(logInfo.Description)
                {
                    Foreground = _textBrush,
                    BaselineAlignment = BaselineAlignment.Center
                });
                runs.Add(new Run(Environment.NewLine));
            }

            // 一次性添加所有日志到UI，减少UI更新次数
            foreach (var run in runs)
            {
                inlines.Add(run);
            }

            // 批量写入文件 - 重要优化：批量构建日志内容，单次写入文件
            Task.Run(() =>
            {
                try
                {
                    Logger.AddLogBatchToFile(logsBatch);
                }
                catch { /* ignored */ }
            });

            // 减少滚动频率，使用Dispatcher降低优先级避免阻塞UI线程
            bool isFinalBatch = Logger.Logs.Count == 0;
            if ((logsBatch.Count > 0 && _scrollViewer != null) && (isFinalBatch || inlines.Count % 200 == 0))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        _scrollViewer.ScrollToEnd();
                    }
                    catch { /* ignored */ }
                }, DispatcherPriority.Background);
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
            LogType.Debug => _debugBrush,
            LogType.Info => _infoBrush,
            LogType.Warn => _warnBrush,
            LogType.Error => _errorBrush,
            LogType.Fatal => _fatalBrush,
            _ => _defaultBrush
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
            Process.Start("explorer.exe", logFolder);
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