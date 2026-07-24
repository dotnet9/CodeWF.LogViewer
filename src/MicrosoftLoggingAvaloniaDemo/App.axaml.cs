using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MicrosoftLoggingAvaloniaDemo.Views;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;

namespace MicrosoftLoggingAvaloniaDemo;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LogContext.SetSource(this, Program.Services.GetRequiredService<LogEventFeed>());
        LogContext.SetLogDirectory(this, Path.Combine(AppContext.BaseDirectory, "Log"));
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Program.Services.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }
}
