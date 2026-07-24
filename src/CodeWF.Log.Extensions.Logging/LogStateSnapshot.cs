using CodeWF.Log.Core;

namespace CodeWF.Log.Extensions.Logging;

internal sealed record LogStateSnapshot(
    string? MessageTemplate,
    IReadOnlyList<LogProperty> Properties,
    string? UserMessage)
{
    public static LogStateSnapshot Capture<TState>(TState state)
    {
        var userMessage = (state as ICodeWFUserLogState)?.UserMessage;
        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            return CaptureProperties(properties) with { UserMessage = userMessage };
        return new LogStateSnapshot(null, [], userMessage);
    }

    public static LogScope CaptureScope(object? scope)
    {
        if (scope is IEnumerable<KeyValuePair<string, object?>> properties)
            return new LogScope(SafeToString(scope), CaptureProperties(properties).Properties);
        return new LogScope(SafeToString(scope), []);
    }

    private static LogStateSnapshot CaptureProperties(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        string? messageTemplate = null;
        var captured = new List<LogProperty>();
        foreach (var property in properties)
        {
            if (property.Key == CodeWFLogPropertyNames.OriginalFormat)
            {
                messageTemplate = property.Value as string;
                continue;
            }
            captured.Add(new LogProperty(property.Key, LogValueFormatter.Capture(property.Value)));
        }
        return new LogStateSnapshot(messageTemplate, captured, null);
    }

    private static string? SafeToString(object? value)
    {
        try { return value?.ToString(); }
        catch { return value?.GetType().FullName; }
    }
}
