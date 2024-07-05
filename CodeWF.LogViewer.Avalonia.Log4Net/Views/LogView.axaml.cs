using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CodeWF.LogViewer.Avalonia.Log4Net.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWF.LogViewer.Avalonia.Log4Net
{
    public partial class LogView : UserControl
    {
        private readonly LogType _levelInConfigFile;
        private const int MaxCount = 1000;
        private readonly SynchronizationContext _synchronizationContext;
        private SelectableTextBlock _textView;
        private ScrollViewer _scrollViewer;
        private IClipboard _clipboard;

        private static readonly Dictionary<LogType, IImmutableSolidColorBrush> LogTypeBrushes =
            new Dictionary<LogType, IImmutableSolidColorBrush>()
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
            _levelInConfigFile = GetLevel();
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
            _textView.Text = string.Empty;
            LogFactory.Instance.Log.LogNotifyEvent -= LogNotifyHandler;
            LogFactory.Instance.Log.LogNotifyEvent += LogNotifyHandler;
        }

        private void LogNotifyHandler(LogInfo logInfo)
        {
            if (_levelInConfigFile > logInfo.Level)
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

        private static LogType GetLevel()
        {
            return (LogType)Enum.Parse(typeof(LogType),
                ((LogManager)LogFactory.Instance.Log).Level.DisplayName.ToLower(), true);
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
            var logFolder = (LogFactory.Instance.Log as LogManager)?.GetLogFilesDirectory();
            if (string.IsNullOrWhiteSpace(logFolder))
            {
                logFolder = AppDomain.CurrentDomain.BaseDirectory;
            }

            try
            {
                System.Diagnostics.Process.Start(logFolder);
            }
            catch
            {
                // ignored
            }
        }
    }
}