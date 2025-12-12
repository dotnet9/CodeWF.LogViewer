using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using CodeWF.Log.Core;
using CodeWF.Log.Core.Extensions;
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
    private IClipboard _clipboard;
    private ContextMenu _contextMenu;

    private bool _isRecording;
    private ScrollViewer _scrollViewer;
    private SelectableTextBlock _textView;
    private readonly CancellationTokenSource _cancellationTokenSource;

    // 修复：使用Brush对象池，避免重复创建
    private static readonly SolidColorBrush GrayBrush = new(Color.Parse("#8C8C8C"));

    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#262626"));
    private static readonly SolidColorBrush DebugBrush = new(Color.Parse("#1890FF"));
    private static readonly SolidColorBrush InfoBrush = new(Color.Parse("#52C41A"));
    private static readonly SolidColorBrush WarnBrush = new(Color.Parse("#FAAD14"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush FatalBrush = new(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#000000"));

    public LogView()
    {
        InitializeComponent();
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

            try
            {
                while (!token.IsCancellationRequested)
                {
                    logsBatch.Clear();

                    while (logsBatch.Count < Logger.BatchProcessSize && Logger.TryDequeue(out var log))
                    {
                        logsBatch.Add(log);
                    }

                    if (logsBatch.Count > 0)
                    {
                        var toFileLogs = logsBatch.Where(log => log.Log2File).ToList();
                        if (toFileLogs.Count != 0)
                        {
                            await Logger.AddLogBatchToFileAsync(toFileLogs);
                        }

                        var toUiLogs = logsBatch.Where(log => log.Log2UI).ToList();
                        if (toUiLogs.Count != 0)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() => UpdateLogUi(toUiLogs));
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(Logger.LogUIDuration), token);
                }
            }
            catch (OperationCanceledException)
            {
                /* 任务被取消，正常退出 */
            }
        });
    }

    /// <summary>
    /// 批量更新日志UI
    /// </summary>
    /// <param name="logsBatch">日志批次</param>
    private void UpdateLogUi(List<LogInfo>? logsBatch)
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

            Process.Start(new ProcessStartInfo("explorer.exe", logFolder)
            {
                UseShellExecute = true
            });
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