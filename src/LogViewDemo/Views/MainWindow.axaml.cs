using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;
using LogViewDemo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LogViewDemo.Views;

public partial class MainWindow : Window
{
    private const string CompactLineTemplate = "{Timestamp:HH:mm:ss} 【{Level:zh}】 {UserMessage}{NewLine}";
    private const string ContextLineTemplate = "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 ({Category}) Event={EventId} Trace={TraceId} {UserMessage} | Message={Message} | Properties={Properties}{NewLine}{Exception}";
    private const string DiagnosticFileTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}";
    private const string ContextFileTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) Event={EventId} Trace={TraceId} {Message} | Properties={Properties} | Scopes={Scopes}{NewLine}{Exception}";

    private readonly DemoLogService _logService;
    private readonly ILineTemplateController _lineTemplateController;
    private readonly IFileOutputTemplateController _fileTemplateController;
    private ComboBox _lineTemplateBox = null!;
    private ComboBox _fileTemplateBox = null!;
    private ComboBox _notificationModeBox = null!;
    private TextBlock _statusText = null!;

    public MainWindow()
        : this(
            Program.Services.GetRequiredService<DemoLogService>(),
            Program.Services.GetRequiredService<ILineTemplateController>(),
            Program.Services.GetRequiredService<IFileOutputTemplateController>())
    {
    }

    public MainWindow(
        DemoLogService logService,
        ILineTemplateController lineTemplateController,
        IFileOutputTemplateController fileTemplateController)
    {
        _logService = logService;
        _lineTemplateController = lineTemplateController;
        _fileTemplateController = fileTemplateController;
        InitializeComponent();
        _lineTemplateBox = this.FindControl<ComboBox>("LineTemplateBox")!;
        _fileTemplateBox = this.FindControl<ComboBox>("FileTemplateBox")!;
        _notificationModeBox = this.FindControl<ComboBox>("NotificationModeBox")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _lineTemplateBox.SelectedIndex = 0;
        _fileTemplateBox.SelectedIndex = 0;
        _notificationModeBox.SelectedIndex = 2;
        Opened += (_, _) => _logService.WriteStartup();
    }

    private void LineTemplateBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_lineTemplateBox is null || _statusText is null) return;
        var template = _lineTemplateBox.SelectedIndex == 1 ? ContextLineTemplate : CompactLineTemplate;
        SetStatus(_lineTemplateController.TryUpdate(template, out var error), error,
            "界面模板已切换，三个 LogView 会立即重新渲染。");
    }

    private void FileTemplateBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_fileTemplateBox is null || _statusText is null) return;
        var template = _fileTemplateBox.SelectedIndex == 1 ? ContextFileTemplate : DiagnosticFileTemplate;
        SetStatus(_fileTemplateController.TryUpdate(template, out var error), error,
            "文件模板已切换，后续日志使用新格式。");
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

    private void WriteAllLevels_OnClick(object? sender, RoutedEventArgs e)
    {
        _logService.WriteAllLevels();
        _statusText.Text = "已输出 Trace 至 Critical；观察三个视图的级别过滤。";
    }

    private void WriteMessageComparison_OnClick(object? sender, RoutedEventArgs e)
    {
        _logService.WriteMessageComparison();
        _statusText.Text = "已输出标准 Message 与 UserMessage 对比；切换完整上下文模板查看差异。";
    }

    private void WriteContext_OnClick(object? sender, RoutedEventArgs e)
    {
        _logService.WriteContextAndException();
        _statusText.Text = "已输出 EventId、结构化属性、Scope、Activity 和异常。";
    }

    private void WriteNotificationError_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !bool.TryParse(value, out var request)) return;
        _logService.WriteNotificationError(request);
        _statusText.Text = request
            ? "已写入 Error 并显式请求通知。"
            : "已写入 Error，但未请求通知。";
    }

    private async void WriteBurst_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        try
        {
            await _logService.WriteBurstAsync(120);
            _statusText.Text = "已并发写入 120 条 Information，界面保持批量刷新。";
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

    private void SetStatus(bool success, string? error, string message)
    {
        _statusText.Text = success ? message : error;
        _statusText.Foreground = success ? Avalonia.Media.Brushes.SeaGreen : Avalonia.Media.Brushes.IndianRed;
    }
}
