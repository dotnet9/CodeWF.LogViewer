using Avalonia;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiProviderAvaloniaDemo.Views;
using Serilog;

namespace MultiProviderAvaloniaDemo;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static string LogDirectory { get; } = Path.GetFullPath("Log");

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

    private static ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
        var serilog = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddSerilog(serilog, dispose: true);
                builder.AddCodeWF();
            })
            .AddTransient<MainWindow>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
