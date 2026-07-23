using Avalonia;
using CodeWF.Log.Core;
using System;
using System.IO;

namespace AvaloniaLogDemo;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Logger.Initialize(new LoggerOptions
        {
            MinimumLevel = LogType.Debug,
            EnableConsole = false,
            RecentUserLogCapacity = 2_000,
            File = new FileLogOptions
            {
                DirectoryPath = Path.Combine(AppContext.BaseDirectory, "Log"),
                BatchSize = 80,
                FlushInterval = TimeSpan.FromMilliseconds(300),
                MaxFileSizeBytes = 5L * 1024 * 1024
            }
        });

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Logger.ShutdownAsync().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
            .WithInterFont()
            .LogToTrace();
}
