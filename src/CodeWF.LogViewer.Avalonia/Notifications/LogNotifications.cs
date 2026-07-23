using Avalonia;
using Avalonia.Controls.Templates;
using CodeWF.Log.Core;
using System;

namespace CodeWF.LogViewer.Avalonia;

/// <summary>
/// 在 Avalonia <see cref="Application"/> 上配置应用级日志通知，不依赖 LogView 或具体窗口。
/// </summary>
public sealed class LogNotifications
{
    public static readonly AttachedProperty<LogNotificationMode> ModeProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, LogNotificationMode>(
            "Mode",
            LogNotificationMode.None);

    public static readonly AttachedProperty<LogType> MinimumLevelProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, LogType>(
            "MinimumLevel",
            LogType.Error);

    public static readonly AttachedProperty<LogType> MaximumLevelProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, LogType>(
            "MaximumLevel",
            LogType.Fatal);

    public static readonly AttachedProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, TimeSpan>(
            "Duration",
            TimeSpan.FromSeconds(10));

    public static readonly AttachedProperty<string?> ApplicationNameProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, string?>("ApplicationName");

    public static readonly AttachedProperty<IDataTemplate?> DesktopContentTemplateProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, IDataTemplate?>("DesktopContentTemplate");

    public static readonly AttachedProperty<DesktopNotificationAttentionMode> AttentionModeProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, DesktopNotificationAttentionMode>(
            "AttentionMode",
            DesktopNotificationAttentionMode.ShakeAndPulse);

    private static readonly AttachedProperty<LogNotificationPresenter?> PresenterProperty =
        AvaloniaProperty.RegisterAttached<LogNotifications, Application, LogNotificationPresenter?>("Presenter");

    static LogNotifications()
    {
        ModeProperty.Changed.AddClassHandler<Application>(OnConfigurationChanged);
        MinimumLevelProperty.Changed.AddClassHandler<Application>(OnConfigurationChanged);
        MaximumLevelProperty.Changed.AddClassHandler<Application>(OnConfigurationChanged);
        DurationProperty.Changed.AddClassHandler<Application>(OnConfigurationChanged);
        ApplicationNameProperty.Changed.AddClassHandler<Application>(OnConfigurationChanged);
        DesktopContentTemplateProperty.Changed.AddClassHandler<Application>(OnConfigurationChanged);
        AttentionModeProperty.Changed.AddClassHandler<Application>(OnConfigurationChanged);
    }

    private LogNotifications()
    {
    }

    public static LogNotificationMode GetMode(Application target) => target.GetValue(ModeProperty);
    public static void SetMode(Application target, LogNotificationMode value) => target.SetValue(ModeProperty, value);

    public static LogType GetMinimumLevel(Application target) => target.GetValue(MinimumLevelProperty);
    public static void SetMinimumLevel(Application target, LogType value) => target.SetValue(MinimumLevelProperty, value);

    public static LogType GetMaximumLevel(Application target) => target.GetValue(MaximumLevelProperty);
    public static void SetMaximumLevel(Application target, LogType value) => target.SetValue(MaximumLevelProperty, value);

    public static TimeSpan GetDuration(Application target) => target.GetValue(DurationProperty);
    public static void SetDuration(Application target, TimeSpan value) => target.SetValue(DurationProperty, value);

    public static string? GetApplicationName(Application target) => target.GetValue(ApplicationNameProperty);
    public static void SetApplicationName(Application target, string? value) =>
        target.SetValue(ApplicationNameProperty, value);

    public static IDataTemplate? GetDesktopContentTemplate(Application target) =>
        target.GetValue(DesktopContentTemplateProperty);

    public static void SetDesktopContentTemplate(Application target, IDataTemplate? value) =>
        target.SetValue(DesktopContentTemplateProperty, value);

    public static DesktopNotificationAttentionMode GetAttentionMode(Application target) =>
        target.GetValue(AttentionModeProperty);

    public static void SetAttentionMode(Application target, DesktopNotificationAttentionMode value) =>
        target.SetValue(AttentionModeProperty, value);

    private static void OnConfigurationChanged(Application target, AvaloniaPropertyChangedEventArgs args)
    {
        var mode = GetMode(target);
        var presenter = target.GetValue(PresenterProperty);
        if (mode == LogNotificationMode.None)
        {
            presenter?.Dispose();
            target.ClearValue(PresenterProperty);
            return;
        }

        if (presenter is null)
        {
            presenter = new LogNotificationPresenter(target);
            target.SetValue(PresenterProperty, presenter);
        }

        presenter.Configure(
            mode,
            GetMinimumLevel(target),
            GetMaximumLevel(target),
            GetDuration(target),
            GetApplicationName(target),
            GetDesktopContentTemplate(target),
            GetAttentionMode(target));
    }
}
