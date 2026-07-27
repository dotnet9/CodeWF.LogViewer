using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;
using FileNotifyDemo.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FileNotifyDemo;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 通知订阅事件 Feed；它不要求窗口中存在 LogView。
        LogContext.SetSource(this, Program.Services.GetRequiredService<LogEventFeed>());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Program.Services.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }
}
