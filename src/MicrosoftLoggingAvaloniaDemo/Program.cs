using Avalonia;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicrosoftLoggingAvaloniaDemo.Services;
using MicrosoftLoggingAvaloniaDemo.Views;

namespace MicrosoftLoggingAvaloniaDemo;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        using var services = BuildServices();
        Services = services;
        Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("MicrosoftLoggingAvaloniaDemo.Startup")
            .LogDebug("CodeWF logging provider initialized before Avalonia XAML is loaded.");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
            .WithInterFont()
            .LogToTrace();

    private static ServiceProvider BuildServices()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "Log");

        return new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddCodeWF(options =>
                {
                    options.File.DirectoryPath = logDirectory;
                    options.File.BatchSize = 50;
                    options.File.FlushInterval = TimeSpan.FromMilliseconds(300);
                    //options.File.OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}";
                    options.File.OutputTemplate =
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message} {Properties}{NewLine}{Exception}";
                    options.Console.Enabled = false;
                });
            })
            .AddSingleton<DeviceLogService>()
            .AddTransient<MainWindow>()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }
}
