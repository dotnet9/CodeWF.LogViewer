using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CodeWF.LogViewer.Avalonia.Extensions;
using System;
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
    private const int BatchProcessSize = 50; // 批量处理的日志数量

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
            var logsBatch = new System.Collections.Generic.List<LogInfo>();
            var token = _cancellationTokenSource.Token;
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    logsBatch.Clear();
                    
                    // 批量收集日志
                    int count = 0;
                    while (count < BatchProcessSize && Logger.TryDequeue(out var log))
                    {
                        if (Logger.Level <= log.Level) // 提前过滤不符合级别的日志
                        {
                            logsBatch.Add(log);
                        }
                        count++;
                    }
                    
                    // 有日志需要处理时批量更新UI
                    if (logsBatch.Count > 0)
                    {
                        _synchronizationContext.Post(o =>
                        {
                            try
                            {
                                UpdateLogUI((System.Collections.Generic.List<LogInfo>)o);
                            }
                            catch { /* ignored */ }
                        }, logsBatch.ToList()); // 传递副本避免并发问题
                    }
                    
                    // 根据队列负载动态调整休眠时间
                    // 使用一个临时变量来避免丢失日志
                    bool hasMoreLogs = false;
                    
                    if (Logger.TryPeek(out _)) // 使用TryPeek安全检查队列是否有日志而不移除
                    {
                        hasMoreLogs = true;
                    }
                    
                    int delayMs = hasMoreLogs ? 10 : 100;
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
            
        var inlines = _textView.Inlines;
        if (inlines == null) return;
        
        try
        {
            // 批量清理超出限制的日志 - 优化版本：批量删除而不是逐条删除
            if (inlines.Count > MaxCount)
            {
                int removeCount = Math.Min(inlines.Count - MaxCount + logsBatch.Count * 4, inlines.Count / 2); // 一次性删除更多日志
                // 重要优化：创建新的Inlines集合代替逐条删除
                var newInlines = new System.Collections.Generic.List<Inline>();
                for (int i = removeCount; i < inlines.Count; i++)
                {
                    newInlines.Add(inlines[i]);
                }
                inlines.Clear();
                foreach (var inline in newInlines)
                {
                    inlines.Add(inline);
                }
            }

            // 批量添加日志到UI
            var runs = new System.Collections.Generic.List<Inline>();
            foreach (var logInfo in logsBatch)
            {
                runs.Add(new Run($"{logInfo.RecordTime}")
                {
                    Foreground = new SolidColorBrush(Color.Parse("#8C8C8C")),
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
                    Foreground = new SolidColorBrush(Color.Parse("#262626")),
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
            
            // 只在批次处理完成后滚动一次 - 优化：移除不必要的文本选择操作
            if (logsBatch.Count > 0 && _scrollViewer != null)
            {
                // 避免不必要的文本选择操作
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
            LogType.Debug => new SolidColorBrush(Color.Parse("#1890FF")),
            LogType.Info => new SolidColorBrush(Color.Parse("#52C41A")),
            LogType.Warn => new SolidColorBrush(Color.Parse("#FAAD14")),
            LogType.Error => new SolidColorBrush(Color.Parse("#FF4D4F")),
            LogType.Fatal => new SolidColorBrush(Color.Parse("#FF4D4F")),
            _ => new SolidColorBrush(Color.Parse("#000000"))
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
        catch(Exception ex)
        {
            Logger.Error($"Open log dir exception, the dir is {logFolder}", ex);
        }
    }

    private void LogScrollViewer_OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) _contextMenu.Open();
    }
}