using Avalonia;
using System;

namespace AvaloniaLogDemo
{
    internal sealed class Program
    {
        // 初始化代码。在调用AppMain之前不要使用任何Avalonia、第三方API或任何依赖SynchronizationContext的代码：此时事物尚未初始化，可能会导致程序崩溃。
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia配置，不要删除；可视化设计器也会使用此配置。
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software]  })
                .WithInterFont()
                .LogToTrace();
    }
}
