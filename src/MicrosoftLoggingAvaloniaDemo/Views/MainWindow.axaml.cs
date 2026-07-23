using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicrosoftLoggingAvaloniaDemo.Services;

namespace MicrosoftLoggingAvaloniaDemo.Views;

public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly DeviceLogService _deviceLogService;

    public MainWindow()
        : this(
            Program.Services.GetRequiredService<ILogger<MainWindow>>(),
            Program.Services.GetRequiredService<DeviceLogService>())
    {
    }

    public MainWindow(ILogger<MainWindow> logger, DeviceLogService deviceLogService)
    {
        _logger = logger;
        _deviceLogService = deviceLogService;
        InitializeComponent();

        Opened += (_, _) =>
        {
            _logger.LogInformation("Main window opened.");
            _logger.LogUserInformation("ILogger<T> Avalonia Demo 已启动。", "ILogger<T> Avalonia demo started.");
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void WriteAllLevels_OnClick(object? sender, RoutedEventArgs e)
    {
        _deviceLogService.WriteAllLevels();
    }

    private void WriteDiagnosticOnly_OnClick(object? sender, RoutedEventArgs e)
    {
        _deviceLogService.WriteDiagnosticOnly();
    }

    private void WriteFriendlyException_OnClick(object? sender, RoutedEventArgs e)
    {
        _deviceLogService.WriteFriendlyException();
    }

    private async void WriteBurst_OnClick(object? sender, RoutedEventArgs e)
    {
        await _deviceLogService.WriteBurstAsync();
    }
}
