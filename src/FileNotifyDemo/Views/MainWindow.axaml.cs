using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileNotifyDemo.Views;

public partial class MainWindow : Window
{
    private const string CompactFileTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message}{NewLine}{Exception}";
    private const string ContextFileTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) Event={EventId} {Message} | Properties={Properties} | Scopes={Scopes}{NewLine}{Exception}";

    private readonly ILogger<MainWindow> _logger;
    private readonly IFileOutputTemplateController _fileTemplateController;
    private TextBlock _logDirectoryText = null!;
    private TextBlock _statusText = null!;
    private ComboBox _fileTemplateBox = null!;

    public MainWindow()
        : this(
            Program.Services.GetRequiredService<ILogger<MainWindow>>(),
            Program.Services.GetRequiredService<IFileOutputTemplateController>())
    {
    }

    public MainWindow(ILogger<MainWindow> logger, IFileOutputTemplateController fileTemplateController)
    {
        _logger = logger;
        _fileTemplateController = fileTemplateController;
        InitializeComponent();
        _logDirectoryText = this.FindControl<TextBlock>("LogDirectoryText")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _fileTemplateBox = this.FindControl<ComboBox>("FileTemplateBox")!;
        _logDirectoryText.Text = $"日志目录：{Program.LogDirectory}";
        _fileTemplateBox.SelectedIndex = 1;
    }

    private void FileTemplateBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_fileTemplateBox is null || _statusText is null) return;
        var template = _fileTemplateBox.SelectedIndex == 1 ? ContextFileTemplate : CompactFileTemplate;
        var success = _fileTemplateController.TryUpdate(template, out var error);
        _statusText.Text = success ? "文件格式已切换，后续日志使用新模板。" : error;
        _statusText.Foreground = success ? Avalonia.Media.Brushes.SeaGreen : Avalonia.Media.Brushes.IndianRed;
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
