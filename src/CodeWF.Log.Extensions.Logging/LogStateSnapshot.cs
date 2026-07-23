using CodeWF.Log.Core;

namespace CodeWF.Log.Extensions.Logging;

internal sealed record LogStateSnapshot(
    string? MessageTemplate,
    IReadOnlyList<LogProperty> Properties,
    UserLogPayload? UserLog)
{
    public static LogStateSnapshot Capture<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            return CaptureProperties(properties);

        return new LogStateSnapshot(null, [], null);
    }

    public static LogScope CaptureScope(object? scope)
    {
        if (scope is IEnumerable<KeyValuePair<string, object?>> properties)
            return new LogScope(scope.ToString(), CaptureProperties(properties).Properties);

        return new LogScope(scope?.ToString(), []);
    }

    private static LogStateSnapshot CaptureProperties(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        string? messageTemplate = null;
        string? userMessage = null;
        var diagnosticProperties = new List<LogProperty>();
        var userProperties = new List<LogProperty>();

        foreach (var property in properties)
        {
            if (property.Key == CodeWFLogPropertyNames.OriginalFormat)
            {
                messageTemplate = property.Value as string;
                continue;
            }

            if (property.Key == CodeWFLogPropertyNames.UserMessage)
            {
                userMessage = property.Value?.ToString();
                continue;
            }

            if (property.Key.StartsWith(CodeWFLogPropertyNames.UserPropertyPrefix, StringComparison.Ordinal))
            {
                var name = property.Key[CodeWFLogPropertyNames.UserPropertyPrefix.Length..];
                if (!string.IsNullOrWhiteSpace(name))
                    userProperties.Add(new LogProperty(
                        name,
                        LogValueFormatter.Capture(property.Value),
                        LogPropertyVisibility.UserSafe));
                continue;
            }

            diagnosticProperties.Add(new LogProperty(property.Key, LogValueFormatter.Capture(property.Value)));
        }

        var userLog = string.IsNullOrWhiteSpace(userMessage)
            ? null
            : new UserLogPayload
            {
                Message = userMessage,
                Properties = userProperties
            };

        return new LogStateSnapshot(messageTemplate, diagnosticProperties, userLog);
    }
}
