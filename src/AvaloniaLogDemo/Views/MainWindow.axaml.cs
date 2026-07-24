using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using CodeWF.Log.Core;
using CodeWF.Log.Avalonia;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace AvaloniaLogDemo.Views;

public partial class MainWindow : Window
{
    private const string CompactLineTemplate = "{Timestamp:HH:mm:ss} [{Level:zh}] {UserMessage}{NewLine}";
    private const string ContextLineTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u3}] ({Category}) Trace={TraceId} {UserMessage} | Message={Message}{NewLine}{Exception}";
    private const string DiagnosticOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message}{NewLine}{Exception}";
    private const string ContextOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) Event={EventId} Trace={TraceId} {Message} | Properties={Properties} | Scopes={Scopes}{NewLine}{Exception}";

    private ResourceInclude? _customNotificationResources;
    private readonly ComboBox _notificationModeBox;
    private readonly ILineTemplateController _lineTemplateController;
    private readonly IFileOutputTemplateController _fileOutputTemplateController;
    private readonly ComboBox _lineTemplatePresetBox;
    private readonly TextBox _lineTemplateEditor;
    private readonly TextBlock _lineTemplateStatus;
    private readonly ComboBox _outputTemplatePresetBox;
    private readonly TextBox _outputTemplateEditor;
    private readonly TextBlock _outputTemplateStatus;
    private bool _updatingLineTemplate;
    private bool _updatingOutputTemplate;
    private int _operation;

    public MainWindow()
    {
        InitializeComponent();
        _notificationModeBox = this.FindControl<ComboBox>("NotificationModeBox")!;
        _lineTemplateController = Logger.Events.LineTemplate;
        _fileOutputTemplateController = Logger.FileOutputTemplate;
        _lineTemplatePresetBox = this.FindControl<ComboBox>("LineTemplatePresetBox")!;
        _lineTemplateEditor = this.FindControl<TextBox>("LineTemplateEditor")!;
        _lineTemplateStatus = this.FindControl<TextBlock>("LineTemplateStatus")!;
        _outputTemplatePresetBox = this.FindControl<ComboBox>("OutputTemplatePresetBox")!;
        _outputTemplateEditor = this.FindControl<TextBox>("OutputTemplateEditor")!;
        _outputTemplateStatus = this.FindControl<TextBlock>("OutputTemplateStatus")!;
        SetLineTemplateEditor(_lineTemplateController.Current);
        SetOutputTemplateEditor(_fileOutputTemplateController.Current ?? DiagnosticOutputTemplate);
        _notificationModeBox.SelectedIndex = 2;
        Opened += (_, _) =>
        {
            Logger.Info($"Avalonia Demo 已于 {DateTime.Now:HH:mm:ss} 启动，三个 LogView 正在订阅同一事件流。");
            Logger.InfoToFile($"Avalonia Demo 技术信息：窗口和日志管线于 {DateTimeOffset.Now:O} 初始化完成。");
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void WriteLevel_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string text } || !Enum.TryParse<LogLevel>(text, out var level)) return;
        var operation = Interlocked.Increment(ref _operation);
        var device = $"PLC-{Random.Shared.Next(1, 100):00}";
        var message = $"操作 {operation}：设备 {device} 在 {DateTime.Now:HH:mm:ss.fff} 产生 {level} 日志，采样值={Random.Shared.NextDouble() * 100:F2}。";
        Logger.Log(level, message, level >= LogLevel.Warning ? $"设备 {device} 状态需要关注（操作 {operation}）。" : null);
    }

    private void WriteAllLevels_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var level in Enum.GetValues<LogLevel>().Where(level => level != LogLevel.None))
        {
            var button = new Button { Tag = level.ToString() };
            WriteLevel_OnClick(button, e);
        }
    }

    private void WriteFriendlyException_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            throw new InvalidOperationException($"Instance validation error at item {Random.Shared.Next(1, 50)}: unsupported PointDirection.");
        }
        catch (Exception ex)
        {
            Logger.Error(
                $@"打开任务失败：E:\TaskFolder\task-{Interlocked.Increment(ref _operation):000}\task.xml",
                ex,
                "无法打开任务“task3”：任务文件内容不正确或与当前版本不兼容，请重新导出任务文件。");
        }
    }

    private void WriteFileOnly_OnClick(object? sender, RoutedEventArgs e)
    {
        Logger.InfoToFile($"文件专用信息：TCP 监听地址=127.0.0.1:{Random.Shared.Next(2000, 9000)}，时间={DateTimeOffset.Now:O}。");
        Logger.ErrorToFile(
            "文件专用异常：后台通知队列清理失败。",
            new IOException("This exception must never appear in UI or notifications."));
    }

    private async void WriteBurst_OnClick(object? sender, RoutedEventArgs e)
    {
        await Task.Run(() => Parallel.ForEach(
            Enumerable.Range(1, 500),
            index => Logger.Info($"批次 {DateTime.Now:HHmmss} 用户日志 #{index:000}，值={Random.Shared.NextDouble():F4}。")));
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
    }

    private void LineTemplatePresetBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingLineTemplate || _lineTemplatePresetBox is null) return;
        if (_lineTemplatePresetBox.SelectedIndex == 0) SetLineTemplateEditor(CompactLineTemplate);
        else if (_lineTemplatePresetBox.SelectedIndex == 1) SetLineTemplateEditor(ContextLineTemplate);
        else return;
        UpdateLineTemplate();
    }

    private void LineTemplateEditor_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_lineTemplateEditor is null || _updatingLineTemplate) return;
        _updatingLineTemplate = true;
        _lineTemplatePresetBox.SelectedIndex = ResolvePreset(_lineTemplateEditor.Text, CompactLineTemplate, ContextLineTemplate);
        _updatingLineTemplate = false;
        UpdateLineTemplate();
    }

    private void UpdateLineTemplate()
    {
        ApplyTemplate(_lineTemplateController.TryUpdate(_lineTemplateEditor.Text ?? string.Empty, out var error), error,
            _lineTemplateStatus, "LineTemplate 已自动更新，三个 LogView 已重新渲染。");
    }

    private void OutputTemplatePresetBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingOutputTemplate || _outputTemplatePresetBox is null) return;
        if (_outputTemplatePresetBox.SelectedIndex == 0) SetOutputTemplateEditor(DiagnosticOutputTemplate);
        else if (_outputTemplatePresetBox.SelectedIndex == 1) SetOutputTemplateEditor(ContextOutputTemplate);
        else return;
        UpdateOutputTemplate();
    }

    private void OutputTemplateEditor_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_outputTemplateEditor is null || _updatingOutputTemplate) return;
        _updatingOutputTemplate = true;
        _outputTemplatePresetBox.SelectedIndex = ResolvePreset(_outputTemplateEditor.Text, DiagnosticOutputTemplate, ContextOutputTemplate);
        _updatingOutputTemplate = false;
        UpdateOutputTemplate();
    }

    private void UpdateOutputTemplate()
    {
        ApplyTemplate(_fileOutputTemplateController.TryUpdate(_outputTemplateEditor.Text, out var error), error,
            _outputTemplateStatus, "OutputTemplate 已自动更新，后续文件日志立即使用新格式。");
    }

    private void SetLineTemplateEditor(string template)
    {
        _updatingLineTemplate = true;
        _lineTemplateEditor.Text = template;
        _lineTemplatePresetBox.SelectedIndex = ResolvePreset(template, CompactLineTemplate, ContextLineTemplate);
        _updatingLineTemplate = false;
        ShowValidation(template, _lineTemplateStatus);
    }

    private void SetOutputTemplateEditor(string template)
    {
        _updatingOutputTemplate = true;
        _outputTemplateEditor.Text = template;
        _outputTemplatePresetBox.SelectedIndex = ResolvePreset(template, DiagnosticOutputTemplate, ContextOutputTemplate);
        _updatingOutputTemplate = false;
        ShowValidation(template, _outputTemplateStatus);
    }

    private static int ResolvePreset(string? template, string first, string second) => template switch
    {
        var value when value == first => 0,
        var value when value == second => 1,
        _ => 2
    };

    private static void ShowValidation(string? template, TextBlock status)
    {
        if (LogTemplateFormatter.TryValidate(template, out var error))
        {
            status.Text = string.Empty;
            return;
        }
        status.Text = error;
        status.Foreground = Avalonia.Media.Brushes.IndianRed;
    }

    private static void ApplyTemplate(bool success, string? error, TextBlock status, string successMessage)
    {
        status.Text = success ? successMessage : error;
        status.Foreground = success ? Avalonia.Media.Brushes.SeaGreen : Avalonia.Media.Brushes.IndianRed;
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
