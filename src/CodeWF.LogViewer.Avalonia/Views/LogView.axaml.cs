using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CodeWF.LogViewer.Avalonia.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Shapes;
using Avalonia.Input;

namespace CodeWF.LogViewer.Avalonia
{
    public partial class LogView : UserControl
    {
        private const int MaxCount = 1000;
        private readonly SynchronizationContext _synchronizationContext;
        private SelectableTextBlock _textView;
        private ScrollViewer _scrollViewer;
        private ContextMenu _contextMenu;
        private IClipboard _clipboard;

        private static readonly Dictionary<LogType, IImmutableSolidColorBrush> LogTypeBrushes =
            new()
            {
                { LogType.Debug, Brushes.LightBlue },
                { LogType.Info, Brushes.Green },
                { LogType.Warn, Brushes.DarkOrange },
                { LogType.Error, Brushes.OrangeRed },
                { LogType.Fatal, Brushes.Red }
            };

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
            if (level == null)
            {
                return;
            }

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

        private bool _isRecording;

        private void RecordLog()
        {
            if (_isRecording)
            {
                return;
            }

            _isRecording = true;

            Task.Run(async () =>
            {
                while (true)
                {
                    while (Logger.TryDequeue(out var log))
                    {
                        LogNotifyHandler(log);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            });
        }

        private void LogNotifyHandler(LogInfo logInfo)
        {
            if (Logger.Level > logInfo.Level)
            {
                return;
            }

            Task.Factory.StartNew(() =>
            {
                _synchronizationContext.Post(o =>
                {
                    var inlines = _textView.Inlines;
                    try
                    {
                        if (inlines?.Count > MaxCount)
                        {
                            inlines.Remove(inlines.First());
                        }

                        var start = _textView.Text.Length;
                        var content =
                            $"{logInfo.RecordTime}: {logInfo.Level.Description()} {logInfo.Description}{Environment.NewLine}";
                        inlines?.Add(new Run(content) { Foreground = LogTypeBrushes[logInfo.Level] });
                        _textView.SelectionStart = start;
                        _textView.SelectionEnd = _textView.Text.Length;
                        _scrollViewer.ScrollToEnd();
                    }
                    catch
                    {
                        // ignored
                    }
                }, null);
            });
        }

        private async void Copy_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_textView.SelectedText.Length > 0 && _clipboard != null)
                {
                    await _clipboard.SetTextAsync(_textView.SelectedText);
                }
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
            var logFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", logFolder);
            }
            catch
            {
                // ignored
            }
        }

        private void LogScrollViewer_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                _contextMenu.Open();
            }
        }
    }
}