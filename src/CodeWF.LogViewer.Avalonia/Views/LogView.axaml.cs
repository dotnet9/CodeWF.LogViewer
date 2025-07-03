using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CodeWF.LogViewer.Avalonia.Extensions;

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

    public LogView()
    {
        InitializeComponent();
        _synchronizationContext = SynchronizationContext.Current;
        Init();
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
            while (true)
            {
                while (Logger.TryDequeue(out var log)) LogNotifyHandler(log);

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        });
    }

    private void LogNotifyHandler(LogInfo logInfo)
    {
        if (Logger.Level > logInfo.Level) return;

        _synchronizationContext.Post(o =>
        {
            var inlines = _textView.Inlines;
            try
            {
                if (inlines?.Count > MaxCount)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var needRemoveElement = inlines.First();
                        if (needRemoveElement != null)
                        {
                            inlines.Remove(needRemoveElement);
                        }
                    }
                }

                var start = _textView.Text.Length;

                inlines?.Add(
                    new Run($"{logInfo.RecordTime}")
                    {
                        Foreground = new SolidColorBrush(Color.Parse("#8C8C8C")),
                        BaselineAlignment = BaselineAlignment.Center
                    });
                inlines?.Add(GetLevelInline(logInfo.Level));
                inlines?.Add(new Run(logInfo.Description)
                {
                    Foreground = new SolidColorBrush(Color.Parse("#262626")),
                    BaselineAlignment = BaselineAlignment.Center
                });
                inlines?.Add(new Run(Environment.NewLine));

                Logger.AddLogToFile(
                    $"{logInfo.RecordTime}: {logInfo.Level.Description()} {logInfo.Description}{Environment.NewLine}");

                _textView.SelectionStart = start;
                _textView.SelectionEnd = _textView.Text.Length;
                _scrollViewer.ScrollToEnd();
            }
            catch
            {
                // ignored
            }
        }, null);
    }

    private Span GetLevelInline(LogType level)
    {
        var content = level.Description();

        // 创建宽度为零的透明文本，用于复制使用
        // TODO：复制还是有问题，会错位
        var zeroWidthText = new Run($"【{content}】")
        {
            Foreground = Brushes.Transparent, FontSize = 0.001
        };

        // 视觉显示的文本，不会被复制使用
        var border = new Border
        {
            BorderBrush = GetLevelForeground(level),
            Background = GetLevelBackground(level),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(8, 0),
            Margin = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = content,
                Foreground = GetLevelForeground(level),
                IsHitTestVisible = false
            }
        };
        var levelSpan = new Span();
        levelSpan.Inlines.Add(zeroWidthText);
        levelSpan.Inlines.Add(border);
        return levelSpan;
    }

    private IBrush GetLevelBackground(LogType level)
    {
        return level switch
        {
            LogType.Debug => new SolidColorBrush(Color.Parse("#E6F7FF")),
            LogType.Info => new SolidColorBrush(Color.Parse("#F6FFED")),
            LogType.Warn => new SolidColorBrush(Color.Parse("#FFF7E6")),
            LogType.Error => new SolidColorBrush(Color.Parse("#FFF1F0")),
            LogType.Fatal => new SolidColorBrush(Color.Parse("#FF4D4F")),
            _ => new SolidColorBrush(Color.Parse("#FFFFFF"))
        };
    }

    private IBrush GetLevelForeground(LogType level)
    {
        return level switch
        {
            LogType.Debug => new SolidColorBrush(Color.Parse("#1890FF")),
            LogType.Info => new SolidColorBrush(Color.Parse("#52C41A")),
            LogType.Warn => new SolidColorBrush(Color.Parse("#FAAD14")),
            LogType.Error => new SolidColorBrush(Color.Parse("#FF4D4F")),
            LogType.Fatal => new SolidColorBrush(Color.Parse("#FFF1F0")),
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
        var logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");

        try
        {
            Process.Start("explorer.exe", logFolder);
        }
        catch
        {
            // ignored
        }
    }

    private void LogScrollViewer_OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) _contextMenu.Open();
    }
}