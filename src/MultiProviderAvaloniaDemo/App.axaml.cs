using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;
using Microsoft.Extensions.DependencyInjection;
using MultiProviderAvaloniaDemo.Views;

namespace MultiProviderAvaloniaDemo;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        LogContext.SetSource(this, Program.Services.GetRequiredService<LogEventFeed>());
        LogContext.SetLogDirectory(this, Program.LogDirectory);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Program.Services.GetRequiredService<MainWindow>();
        base.OnFrameworkInitializationCompleted();
    }
}
