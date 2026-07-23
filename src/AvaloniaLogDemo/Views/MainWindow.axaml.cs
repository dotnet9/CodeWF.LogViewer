using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using CodeWF.Log.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AvaloniaLogDemo.Views;

public partial class MainWindow : Window
{
    private ResourceInclude? _customNotificationResources;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            Logger.Info("Avalonia Demo 已启动，三个 LogView 正在订阅同一份用户日志。");
            Logger.InfoToFile("Avalonia Demo 技术信息：窗口和日志管线初始化完成。");
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void WriteAllLevels_OnClick(object? sender, RoutedEventArgs e)
    {
        Logger.Debug("调试信息：缓存扫描完成。", "运行环境检查完成。");
        Logger.Info("设备服务已启动，正在等待客户端连接。");
        Logger.Warn("设备响应时间达到 850ms。", "设备响应较慢，请检查网络连接。");
        Logger.Error("数据库连接已中断：endpoint=127.0.0.1:5020。", userMessage: "数据库连接已中断，请检查数据库服务是否运行。");
        Logger.Fatal("运行所需模型无法加载。", userMessage: "模型文件无法加载，程序不能继续运行，请检查模型文件是否完整。");
    }

    private void WriteFriendlyException_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            throw new InvalidOperationException(
                "Instance validation error: '0' is not a valid value for PointDirection.");
        }
        catch (Exception ex)
        {
            Logger.Error(
                @"打开任务失败：E:\TaskFolder\task3\task.xml",
                ex,
                "无法打开任务“task3”：任务文件内容不正确或与当前版本不兼容，请重新导出任务文件。");
        }
    }

    private void WriteFileOnly_OnClick(object? sender, RoutedEventArgs e)
    {
        Logger.InfoToFile("文件专用信息：TCP 监听地址=127.0.0.1:2700。");
        Logger.ErrorToFile(
            "文件专用异常：后台通知队列清理失败。",
            new IOException("This exception must never appear in UI or notifications."));
    }

    private async void WriteBurst_OnClick(object? sender, RoutedEventArgs e)
    {
        await Task.Run(() => Parallel.ForEach(
            Enumerable.Range(1, 500),
            index => Logger.Info($"批量用户日志 #{index:000}。")));
    }

    private void ToggleNotificationTheme_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not { } application || sender is not Button button) return;

        var dictionaries = application.Resources.MergedDictionaries;
        if (_customNotificationResources is null)
        {
            _customNotificationResources = new ResourceInclude(
                new Uri("avares://AvaloniaLogDemo/Styles/"))
            {
                Source = new Uri("avares://AvaloniaLogDemo/Styles/CustomNotificationResources.axaml")
            };
            dictionaries.Add(_customNotificationResources);
            button.Content = "恢复默认通知样式";
            Logger.Info("已启用 Demo 自定义通知样式。");
            return;
        }

        dictionaries.Remove(_customNotificationResources);
        _customNotificationResources = null;
        button.Content = "切换自定义通知样式";
        Logger.Info("已恢复日志组件默认通知样式。 ");
    }
}
