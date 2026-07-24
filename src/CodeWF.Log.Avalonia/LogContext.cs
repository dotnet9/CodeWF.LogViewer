using Avalonia;
using CodeWF.Log.Core;

namespace CodeWF.Log.Avalonia;

public static class LogContext
{
    public static readonly AttachedProperty<LogEventFeed?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Application, AvaloniaObject, LogEventFeed?>("Source");
    public static readonly AttachedProperty<string?> LogDirectoryProperty =
        AvaloniaProperty.RegisterAttached<Application, AvaloniaObject, string?>("LogDirectory");

    public static LogEventFeed? GetSource(AvaloniaObject target) => target.GetValue(SourceProperty);
    public static void SetSource(AvaloniaObject target, LogEventFeed? value) => target.SetValue(SourceProperty, value);
    public static string? GetLogDirectory(AvaloniaObject target) => target.GetValue(LogDirectoryProperty);
    public static void SetLogDirectory(AvaloniaObject target, string? value) => target.SetValue(LogDirectoryProperty, value);
}
