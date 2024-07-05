using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CodeWF.LogViewer.Avalonia.Log4Net;

namespace AvaloniaLog4Net.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AddDebugLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Debug("Debug");
        }

        private void AddInfoLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Info("Info");
        }

        private void AddWarnLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Warn("Warn");
        }

        private void AddErrorLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Error("Error");
        }

        private void AddFatalLog_OnClick(object? sender, RoutedEventArgs e)
        {
            LogFactory.Instance.Log.Fatal("Fatal");
        }
    }
}