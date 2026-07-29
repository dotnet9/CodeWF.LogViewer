using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SerilogDemo.Views;

public partial class MainWindow : Window
{
    private const string CompactTemplate = "{Timestamp:HH:mm:ss} 【{Level:zh}】 {UserMessage}{NewLine}";
    private const string ContextTemplate = "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 ({Category}) Event={EventId} Trace={TraceId} {UserMessage} | Message={Message} | Properties={Properties}{NewLine}{Exception}";

    private readonly ILogger<MainWindow> _logger;
    private readonly ILineTemplateController _lineTemplateController;
    private ComboBox _lineTemplateBox = null!;
    private ComboBox _notificationModeBox = null!;
    private TextBlock _statusText = null!;
    private int _operation;

    public MainWindow()
        : this(
            Program.Services.GetRequiredService<ILogger<MainWindow>>(),
            Program.Services.GetRequiredService<ILineTemplateController>())
    {
    }

    public MainWindow(ILogger<MainWindow> logger, ILineTemplateController lineTemplateController)
    {
        _logger = logger;
        _lineTemplateController = lineTemplateController;
        InitializeComponent();
        _lineTemplateBox = this.FindControl<ComboBox>("LineTemplateBox")!;
        _notificationModeBox = this.FindControl<ComboBox>("NotificationModeBox")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _lineTemplateBox.SelectedIndex = 0;
        _notificationModeBox.SelectedIndex = 1;
        Opened += (_, _) => _logger.LogInformation("SerilogDemo opened at {OpenedAt}", DateTimeOffset.Now);
    }

    private void LineTemplateBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_lineTemplateBox is null || _statusText is null) return;
        var template = _lineTemplateBox.SelectedIndex == 1 ? ContextTemplate : CompactTemplate;
        var success = _lineTemplateController.TryUpdate(template, out var error);
        _statusText.Text = success ? "CodeWF 界面模板已切换；Serilog 文件格式保持独立。" : error;
        _statusText.Foreground = success ? Avalonia.Media.Brushes.SeaGreen : Avalonia.Media.Brushes.IndianRed;
    }

    private void NotificationModeBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Application.Current is not { } application || _notificationModeBox is null) return;
        LogNotifications.SetMode(application, _notificationModeBox.SelectedIndex switch
        {
            1 => LogNotificationMode.InApp,
            2 => LogNotificationMode.DesktopWindow,
            _ => LogNotificationMode.None
        });
        if (_statusText is not null) _statusText.Text = $"通知模式：{_notificationModeBox.Text}";
    }

    private void WriteLevels_OnClick(object? sender, RoutedEventArgs e)
    {
        var operation = NextOperation();
        _logger.LogInformation("Operation {Operation} sampled device {DeviceId}", operation, "PLC-07");
        _logger.LogWarning("Operation {Operation} response took {Elapsed} ms", operation, 920);
        _logger.LogError("Operation {Operation} connection attempt failed", operation);
        _statusText.Text = "已向 Serilog 和 CodeWF 同时输出 Information、Warning、Error；普通 Error 不弹窗。";
    }

    private void WriteUserComparison_OnClick(object? sender, RoutedEventArgs e)
    {
        var operation = NextOperation();
        _logger.LogWarning("Task {TaskName} response exceeded {Elapsed} ms", $"task-{operation:000}", 1100);
        _logger.LogUserWarning(
            $"任务“task-{operation:000}”响应较慢，请稍后重试。",
            "Task {TaskName} response exceeded {Elapsed} ms",
            $"task-{operation:000}", 1100);
        _statusText.Text = "已输出 Message / UserMessage 对比；Serilog 不接收 CodeWF 私有 UserMessage 属性。";
    }

    private void WriteContext_OnClick(object? sender, RoutedEventArgs e)
    {
        var operation = NextOperation();
        using var activity = new Activity("SerilogDemo.DeviceRead").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["Operation"] = operation,
            ["Station"] = "Station-3"
        });
        _logger.LogWarning(new EventId(3201, "DeviceLatency"),
            "Device {DeviceId} response took {Elapsed} ms", "PLC-07", 1280);
        _statusText.Text = "已输出 EventId、结构化属性、Scope 和 Activity；切换完整上下文模板查看。";
    }

    private void WriteNotificationError_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !bool.TryParse(value, out var request)) return;
        var operation = NextOperation();
        if (request)
        {
            _logger.LogUserNotification(
                LogLevel.Error,
                "设备连接已中断，请检查服务状态。",
                "Operation {Operation} explicitly requested a notification",
                operation);
        }
        else
        {
            _logger.LogError("Operation {Operation} failed without requesting a notification", operation);
        }
        _statusText.Text = request ? "已显式请求 Error 通知。" : "已写入普通 Error，不会弹窗。";
    }

    private async void WriteBurst_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        try
        {
            var batch = Guid.NewGuid().ToString("N")[..8];
            await Task.Run(() => Parallel.ForEach(Enumerable.Range(1, 120), index =>
                _logger.LogInformation("Batch {BatchId} item {Index}/{Total}", batch, index, 120)));
            _statusText.Text = "已并发写入 120 条日志；两个 Provider 同时收到事件。";
        }
        finally
        {
            if (sender is Button completedButton) completedButton.IsEnabled = true;
        }
    }

    private void OpenLogDirectory_OnClick(object? sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Program.LogDirectory);
        Process.Start(new ProcessStartInfo { FileName = Program.LogDirectory, UseShellExecute = true });
    }

    private int NextOperation() => Interlocked.Increment(ref _operation);
}
