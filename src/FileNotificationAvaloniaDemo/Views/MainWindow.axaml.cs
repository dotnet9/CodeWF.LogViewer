using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileNotificationAvaloniaDemo.Views;

public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private TextBlock _logDirectoryText = null!;
    private TextBlock _statusText = null!;

    public MainWindow()
        : this(Program.Services.GetRequiredService<ILogger<MainWindow>>())
    {
    }

    public MainWindow(ILogger<MainWindow> logger)
    {
        _logger = logger;
        InitializeComponent();
        _logDirectoryText = this.FindControl<TextBlock>("LogDirectoryText")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _logDirectoryText.Text = $"日志目录：{Program.LogDirectory}";
    }

    private void WriteErrorOnly_OnClick(object? sender, RoutedEventArgs e)
    {
        _logger.LogError("普通连接错误示例：只写日志，不请求系统通知。Operation={Operation}", "ErrorOnly");
        _statusText.Text = "已写入 Error 日志；未请求通知，因此不会弹窗。";
    }

    private void WriteImportantError_OnClick(object? sender, RoutedEventArgs e)
    {
        _logger.LogUserNotification(
            LogLevel.Error,
            "设备服务连接已中断，请检查服务状态。",
            "Important connection error example. Operation={Operation}",
            "ErrorWithNotification");
        _statusText.Text = "已写入 Error 日志并显式请求通知；应显示桌面通知。";
    }

    private void WriteWarningNotification_OnClick(object? sender, RoutedEventArgs e)
    {
        _logger.LogUserNotification(
            LogLevel.Warning,
            "设备服务正在重试连接。",
            "Connection retry warning example. Operation={Operation}",
            "WarningBelowThreshold");
        _statusText.Text = "已写入 Warning 日志并请求通知；低于 Error 阈值，因此不会弹窗。";
    }

    private void OpenLogDirectory_OnClick(object? sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Program.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = Program.LogDirectory,
            UseShellExecute = true
        });
    }
}
