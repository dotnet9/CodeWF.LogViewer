using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicrosoftLoggingAvaloniaDemo.Services;

namespace MicrosoftLoggingAvaloniaDemo.Views;

public partial class MainWindow : Window
{
    private const string CompactTemplate = "{Timestamp:HH:mm:ss} [{Level:zh}] {UserMessage}{NewLine}";
    private const string ContextTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u3}] ({Category}) Event={EventId} Trace={TraceId} {UserMessage} | Message={Message} | Properties={Properties}{NewLine}{Exception}";
    private const string DiagnosticOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message} {Properties}{NewLine}{Exception}";
    private const string ContextOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) Event={EventId} Trace={TraceId} {Message} | Properties={Properties} | Scopes={Scopes}{NewLine}{Exception}";

    private static readonly CodeWFLogEvent PreviewEvent = new()
    {
        Sequence = 1,
        Timestamp = new DateTimeOffset(2026, 7, 24, 10, 30, 15, 123, TimeSpan.FromHours(8)),
        Level = LogLevel.Warning,
        CategoryName = "Demo.DeviceService",
        EventId = new EventId(2101, "DeviceLatency"),
        MessageTemplate = "Device {DeviceId} response took {Elapsed} ms",
        Message = "Device PLC-07 response took 865 ms",
        UserMessage = "设备 PLC-07 响应较慢，请检查网络连接。",
        Properties =
        [
            new LogProperty("DeviceId", new ScalarLogValue("PLC-07")),
            new LogProperty("Elapsed", new ScalarLogValue(865))
        ],
        TraceId = "8b72a93d7bc9416eae4c5608e0d42001"
    };

    private readonly ILogger<MainWindow> _logger;
    private readonly DeviceLogService _deviceLogService;
    private readonly ILineTemplateController _lineTemplateController;
    private readonly IFileOutputTemplateController _fileOutputTemplateController;
    private ComboBox _templatePresetBox = null!;
    private TextBox _templateEditor = null!;
    private TextBlock _templatePreview = null!;
    private TextBlock _templateError = null!;
    private ComboBox _notificationModeBox = null!;
    private ComboBox _outputTemplatePresetBox = null!;
    private TextBox _outputTemplateEditor = null!;
    private TextBlock _outputTemplatePreview = null!;
    private TextBlock _outputTemplateError = null!;
    private bool _updatingTemplateEditor;
    private bool _updatingOutputTemplateEditor;

    public MainWindow()
        : this(
            Program.Services.GetRequiredService<ILogger<MainWindow>>(),
            Program.Services.GetRequiredService<DeviceLogService>(),
            Program.Services.GetRequiredService<ILineTemplateController>(),
            Program.Services.GetRequiredService<IFileOutputTemplateController>())
    {
    }

    public MainWindow(
        ILogger<MainWindow> logger,
        DeviceLogService deviceLogService,
        ILineTemplateController lineTemplateController,
        IFileOutputTemplateController fileOutputTemplateController)
    {
        _logger = logger;
        _deviceLogService = deviceLogService;
        _lineTemplateController = lineTemplateController;
        _fileOutputTemplateController = fileOutputTemplateController;
        InitializeComponent();
        _templatePresetBox = this.FindControl<ComboBox>("TemplatePresetBox")!;
        _templateEditor = this.FindControl<TextBox>("TemplateEditor")!;
        _templatePreview = this.FindControl<TextBlock>("TemplatePreview")!;
        _templateError = this.FindControl<TextBlock>("TemplateError")!;
        _notificationModeBox = this.FindControl<ComboBox>("NotificationModeBox")!;
        _outputTemplatePresetBox = this.FindControl<ComboBox>("OutputTemplatePresetBox")!;
        _outputTemplateEditor = this.FindControl<TextBox>("OutputTemplateEditor")!;
        _outputTemplatePreview = this.FindControl<TextBlock>("OutputTemplatePreview")!;
        _outputTemplateError = this.FindControl<TextBlock>("OutputTemplateError")!;
        SetTemplateEditor(_lineTemplateController.Current, ResolvePreset(_lineTemplateController.Current));
        var outputTemplate = _fileOutputTemplateController.Current ?? DiagnosticOutputTemplate;
        SetOutputTemplateEditor(outputTemplate, ResolveOutputPreset(outputTemplate));
        _notificationModeBox.SelectedIndex = 2;

        Opened += (_, _) =>
        {
            _logger.LogInformation("Main window opened at {OpenedAt}", DateTimeOffset.Now);
            _logger.LogUserInformation(
                $"ILogger<T> Avalonia Demo 已于 {DateTime.Now:HH:mm:ss} 启动。",
                "ILogger<T> Avalonia demo started at {StartedAt}",
                DateTimeOffset.Now);
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void TemplatePresetBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingTemplateEditor || _templatePresetBox is null) return;
        if (_templatePresetBox.SelectedIndex == 0) SetTemplateEditor(CompactTemplate, 0);
        else if (_templatePresetBox.SelectedIndex == 1) SetTemplateEditor(ContextTemplate, 1);
        else return;
        UpdateLineTemplate();
    }

    private void TemplateEditor_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_templateEditor is null) return;
        if (_updatingTemplateEditor) return;
        _updatingTemplateEditor = true;
        _templatePresetBox.SelectedIndex = ResolvePreset(_templateEditor.Text ?? string.Empty);
        _updatingTemplateEditor = false;
        UpdateTemplatePreview();
        UpdateLineTemplate();
    }

    private void UpdateLineTemplate()
    {
        if (_lineTemplateController.TryUpdate(_templateEditor.Text ?? string.Empty, out var error))
        {
            _templateError.Text = "LineTemplate 已自动更新；LogView 已重新渲染，后续 Console 与通知同步使用。";
            _templateError.Foreground = Avalonia.Media.Brushes.SeaGreen;
            return;
        }

        _templateError.Text = error;
        _templateError.Foreground = Avalonia.Media.Brushes.IndianRed;
    }

    private void RestoreTemplate_OnClick(object? sender, RoutedEventArgs e)
    {
        SetTemplateEditor(CompactTemplate, 0);
        UpdateLineTemplate();
    }

    private void OutputTemplatePresetBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingOutputTemplateEditor || _outputTemplatePresetBox is null) return;
        if (_outputTemplatePresetBox.SelectedIndex == 0) SetOutputTemplateEditor(DiagnosticOutputTemplate, 0);
        else if (_outputTemplatePresetBox.SelectedIndex == 1) SetOutputTemplateEditor(ContextOutputTemplate, 1);
        else return;
        UpdateOutputTemplate();
    }

    private void OutputTemplateEditor_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_outputTemplateEditor is null) return;
        if (_updatingOutputTemplateEditor) return;
        _updatingOutputTemplateEditor = true;
        _outputTemplatePresetBox.SelectedIndex = ResolveOutputPreset(_outputTemplateEditor.Text ?? string.Empty);
        _updatingOutputTemplateEditor = false;
        UpdateOutputTemplatePreview();
        UpdateOutputTemplate();
    }

    private void UpdateOutputTemplate()
    {
        if (_fileOutputTemplateController.TryUpdate(_outputTemplateEditor.Text, out var error))
        {
            _outputTemplateError.Text = "OutputTemplate 已自动更新；后续文件日志立即使用新格式。";
            _outputTemplateError.Foreground = Avalonia.Media.Brushes.SeaGreen;
            return;
        }
        _outputTemplateError.Text = error;
        _outputTemplateError.Foreground = Avalonia.Media.Brushes.IndianRed;
    }

    private void RestoreOutputTemplate_OnClick(object? sender, RoutedEventArgs e)
    {
        SetOutputTemplateEditor(DiagnosticOutputTemplate, 0);
        UpdateOutputTemplate();
    }

    private void NotificationModeBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Application.Current is not { } application || _notificationModeBox is null) return;
        var mode = _notificationModeBox.SelectedIndex switch
        {
            1 => LogNotificationMode.InApp,
            2 => LogNotificationMode.DesktopWindow,
            _ => LogNotificationMode.None
        };
        LogNotifications.SetMode(application, mode);
    }

    private void WriteLevel_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text } && Enum.TryParse<LogLevel>(text, out var level))
            _deviceLogService.WriteLevel(level);
    }

    private void WriteAllLevels_OnClick(object? sender, RoutedEventArgs e) => _deviceLogService.WriteAllLevels();
    private void WriteMessageComparison_OnClick(object? sender, RoutedEventArgs e) => _deviceLogService.WriteMessageComparison();
    private void WriteFriendlyException_OnClick(object? sender, RoutedEventArgs e) => _deviceLogService.WriteFriendlyException();
    private void WriteContext_OnClick(object? sender, RoutedEventArgs e) => _deviceLogService.WriteContext();
    private void WriteLoggerMessage_OnClick(object? sender, RoutedEventArgs e) => _deviceLogService.WriteLoggerMessage();
    private async void WriteBurst_OnClick(object? sender, RoutedEventArgs e) => await _deviceLogService.WriteBurstAsync();

    private void SetTemplateEditor(string template, int presetIndex)
    {
        _updatingTemplateEditor = true;
        _templateEditor.Text = template;
        _templatePresetBox.SelectedIndex = presetIndex;
        _updatingTemplateEditor = false;
        UpdateTemplatePreview();
    }

    private void UpdateTemplatePreview()
    {
        if (_templatePreview is null || _templateEditor is null) return;
        var template = _templateEditor.Text ?? string.Empty;
        if (!LogTemplateFormatter.TryValidate(template, out var error))
        {
            _templatePreview.Text = "模板无效，当前生效模板不会被修改。";
            _templateError.Text = error;
            _templateError.Foreground = Avalonia.Media.Brushes.IndianRed;
            return;
        }

        _templatePreview.Text = LogTemplateFormatter.Format(PreviewEvent, template, "yyyy-MM-dd HH:mm:ss.fff");
        _templateError.Text = string.Empty;
    }

    private void SetOutputTemplateEditor(string template, int presetIndex)
    {
        _updatingOutputTemplateEditor = true;
        _outputTemplateEditor.Text = template;
        _outputTemplatePresetBox.SelectedIndex = presetIndex;
        _updatingOutputTemplateEditor = false;
        UpdateOutputTemplatePreview();
    }

    private void UpdateOutputTemplatePreview()
    {
        if (_outputTemplatePreview is null || _outputTemplateEditor is null) return;
        var template = _outputTemplateEditor.Text ?? string.Empty;
        if (!LogTemplateFormatter.TryValidate(template, out var error))
        {
            _outputTemplatePreview.Text = "模板无效，当前文件模板不会被修改。";
            _outputTemplateError.Text = error;
            _outputTemplateError.Foreground = Avalonia.Media.Brushes.IndianRed;
            return;
        }
        _outputTemplatePreview.Text = LogTemplateFormatter.Format(PreviewEvent, template, "yyyy-MM-dd HH:mm:ss.fff");
        _outputTemplateError.Text = string.Empty;
    }

    private static int ResolvePreset(string template) => template switch
    {
        CompactTemplate => 0,
        ContextTemplate => 1,
        _ => 2
    };

    private static int ResolveOutputPreset(string template) => template switch
    {
        DiagnosticOutputTemplate => 0,
        ContextOutputTemplate => 1,
        _ => 2
    };
}
