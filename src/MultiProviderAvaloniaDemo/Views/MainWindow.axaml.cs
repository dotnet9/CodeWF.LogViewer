using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MultiProviderAvaloniaDemo.Views;

public partial class MainWindow : Window
{
    private const string CompactLineTemplate = "{Timestamp:HH:mm:ss} [{Level:zh}] ({Category}) {UserMessage}{NewLine}";
    private const string ContextLineTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u3}] ({Category}) Event={EventId} Trace={TraceId} {UserMessage} | Message={Message} | Properties={Properties}{NewLine}{Exception}";

    private readonly ILogger<MainWindow> _logger;
    private readonly ILineTemplateController _lineTemplateController;
    private ComboBox _notificationModeBox = null!;
    private ComboBox _lineTemplatePresetBox = null!;
    private TextBox _lineTemplateEditor = null!;
    private TextBlock _lineTemplateStatus = null!;
    private bool _updatingLineTemplate;
    private int _operation;

    public MainWindow() : this(
        Program.Services.GetRequiredService<ILogger<MainWindow>>(),
        Program.Services.GetRequiredService<ILineTemplateController>()) { }

    public MainWindow(ILogger<MainWindow> logger, ILineTemplateController lineTemplateController)
    {
        _logger = logger;
        _lineTemplateController = lineTemplateController;
        AvaloniaXamlLoader.Load(this);
        _notificationModeBox = this.FindControl<ComboBox>("NotificationModeBox")!;
        _lineTemplatePresetBox = this.FindControl<ComboBox>("LineTemplatePresetBox")!;
        _lineTemplateEditor = this.FindControl<TextBox>("LineTemplateEditor")!;
        _lineTemplateStatus = this.FindControl<TextBlock>("LineTemplateStatus")!;
        SetLineTemplateEditor(_lineTemplateController.Current);
        _notificationModeBox.SelectedIndex = 1;
        Opened += (_, _) => _logger.LogInformation("Multi-provider window opened at {OpenedAt}", DateTimeOffset.Now);
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
        _lineTemplatePresetBox.SelectedIndex = ResolveLinePreset(_lineTemplateEditor.Text);
        _updatingLineTemplate = false;
        UpdateLineTemplate();
    }

    private void UpdateLineTemplate()
    {
        var success = _lineTemplateController.TryUpdate(_lineTemplateEditor.Text ?? string.Empty, out var error);
        _lineTemplateStatus.Text = success ? "LineTemplate 已自动更新，LogView 与后续通知同步更新。" : error;
        _lineTemplateStatus.Foreground = success ? Avalonia.Media.Brushes.SeaGreen : Avalonia.Media.Brushes.IndianRed;
    }

    private void SetLineTemplateEditor(string template)
    {
        _updatingLineTemplate = true;
        _lineTemplateEditor.Text = template;
        _lineTemplatePresetBox.SelectedIndex = ResolveLinePreset(template);
        _updatingLineTemplate = false;
        _lineTemplateStatus.Text = string.Empty;
    }

    private static int ResolveLinePreset(string? template) => template switch
    {
        CompactLineTemplate => 0,
        ContextLineTemplate => 1,
        _ => 2
    };

    private void WriteLevel_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string text } || !Enum.TryParse<LogLevel>(text, out var level)) return;
        var operation = Interlocked.Increment(ref _operation);
        _logger.Log(level, new EventId(3001, "MultiProvider"),
            "Operation {Operation} sampled device {DeviceId} at {SampledAt}",
            operation, $"PLC-{Random.Shared.Next(1, 100):00}", DateTimeOffset.Now);
    }

    private void WriteUserInformation_OnClick(object? sender, RoutedEventArgs e)
    {
        var operation = Interlocked.Increment(ref _operation);
        _logger.LogUserInformation(
            $"操作 {operation} 已完成，设备数据已刷新。",
            "Operation {Operation} refreshed device {DeviceId}",
            operation, $"PLC-{Random.Shared.Next(1, 100):00}");
    }

    private void WriteUserError_OnClick(object? sender, RoutedEventArgs e)
    {
        var operation = Interlocked.Increment(ref _operation);
        var exception = new IOException($"TCP connection reset during operation {operation}.");
        _logger.LogUserError(
            exception,
            $"操作 {operation} 失败，请检查设备连接后重试。",
            "Operation {Operation} failed for endpoint {Endpoint}",
            operation, $"10.0.0.{Random.Shared.Next(2, 240)}:502");
    }

    private void WriteContext_OnClick(object? sender, RoutedEventArgs e)
    {
        var operation = Interlocked.Increment(ref _operation);
        using var activity = new Activity("MultiProvider.DeviceRead").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["Operation"] = operation,
            ["Station"] = $"Station-{Random.Shared.Next(1, 10)}"
        });
        _logger.LogWarning(new EventId(3201, "DeviceLatency"),
            "Device {DeviceId} response took {ElapsedMilliseconds} ms",
            $"PLC-{Random.Shared.Next(1, 100):00}", Random.Shared.Next(700, 1_500));
    }

    private async void WriteBurst_OnClick(object? sender, RoutedEventArgs e)
    {
        var batch = Guid.NewGuid().ToString("N")[..8];
        await Task.Run(() => Parallel.ForEach(Enumerable.Range(1, 300), index =>
        {
            _logger.LogInformation("Batch {BatchId} item {Index}/{Total}", batch, index, 300);
            if (index % 20 == 0)
                _logger.LogError("Batch {BatchId} notification checkpoint {Index}/{Total}", batch, index, 300);
        }));
    }
}
