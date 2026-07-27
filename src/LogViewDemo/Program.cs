using Avalonia;
using CodeWF.Log.Extensions.Logging;
using LogViewDemo.Services;
using LogViewDemo.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogViewDemo;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static string LogDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Log");

    [STAThread]
    public static void Main(string[] args)
    {
        using var services = BuildServices();
        Services = services;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
            .WithInterFont()
            .LogToTrace();

    private static ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddCodeWF(options =>
                {
                    options.File.DirectoryPath = LogDirectory;
                    options.File.BatchSize = 80;
                    options.File.FlushInterval = TimeSpan.FromMilliseconds(300);
                    options.File.MaxFileSizeBytes = 5L * 1024 * 1024;
                    options.File.OutputTemplate =
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}";
                    options.Console.Enabled = false;
                    options.EventFeed.RecentCapacity = 2_000;
                    options.Capture.ActivityTags = true;
                    options.Capture.ActivityBaggage = true;
                });
            })
            .AddSingleton<DemoLogService>()
            .AddTransient<MainWindow>()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
}
