using CodeWF.Log.Core.Extensions;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace CodeWF.Log.Core;

internal static class LogOutputTemplateFormatter
{
    public static string Format(CodeWFLogEvent logEvent, string outputTemplate, string fallbackTimestampFormat)
    {
        var builder = new StringBuilder(outputTemplate.Length + logEvent.Message.Length + 32);
        for (var index = 0; index < outputTemplate.Length; index++)
        {
            var current = outputTemplate[index];
            if (current == '{')
            {
                if (index + 1 < outputTemplate.Length && outputTemplate[index + 1] == '{')
                {
                    builder.Append('{');
                    index++;
                    continue;
                }

                var end = outputTemplate.IndexOf('}', index + 1);
                if (end > index)
                {
                    AppendToken(builder, logEvent, outputTemplate[(index + 1)..end], fallbackTimestampFormat);
                    index = end;
                    continue;
                }
            }

            if (current == '}' && index + 1 < outputTemplate.Length && outputTemplate[index + 1] == '}')
            {
                builder.Append('}');
                index++;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    public static string FormatProperties(IReadOnlyList<LogProperty> properties)
    {
        return properties.Count == 0
            ? string.Empty
            : string.Join(", ", properties.Select(property =>
                $"{property.Name}={LogValueFormatter.ToDisplayString(property.Value)}"));
    }

    public static string FormatScopes(IReadOnlyList<LogScope> scopes)
    {
        if (scopes.Count == 0) return string.Empty;

        return string.Join(" => ", scopes.Select(scope =>
        {
            var properties = FormatProperties(scope.Properties);
            if (string.IsNullOrWhiteSpace(scope.Text)) return properties;
            return string.IsNullOrWhiteSpace(properties) ? scope.Text : $"{scope.Text} {{{properties}}}";
        }));
    }

    public static string FormatActivity(CodeWFLogEvent logEvent)
    {
        var parts = new List<string>(4);
        AddPart(parts, "TraceId", logEvent.TraceId);
        AddPart(parts, "SpanId", logEvent.SpanId);
        AddPart(parts, "ParentId", logEvent.ParentId);
        AddPart(parts, "TraceState", logEvent.TraceState);
        return string.Join(" ", parts);
    }

    private static void AppendToken(
        StringBuilder builder,
        CodeWFLogEvent logEvent,
        string token,
        string fallbackTimestampFormat)
    {
        var (name, format) = SplitToken(token);
        builder.Append(name switch
        {
            "Timestamp" => FormatTimestamp(logEvent.Timestamp, format, fallbackTimestampFormat),
            "Level" => FormatLevel(logEvent.Level, format),
            "Category" or "CategoryName" => logEvent.CategoryName,
            "EventId" => FormatEventId(logEvent.EventId),
            "EventName" => logEvent.EventId.Name,
            "Message" => logEvent.Message,
            "MessageTemplate" => logEvent.MessageTemplate,
            "UserMessage" => logEvent.UserLog?.Message,
            "Properties" => FormatProperties(logEvent.Properties),
            "UserProperties" => FormatProperties(logEvent.UserLog?.Properties ?? []),
            "Scopes" => FormatScopes(logEvent.Scopes),
            "Activity" => FormatActivity(logEvent),
            "TraceId" => logEvent.TraceId,
            "SpanId" => logEvent.SpanId,
            "ParentId" => logEvent.ParentId,
            "TraceState" => logEvent.TraceState,
            "Exception" => logEvent.Exception?.ToString(),
            "NewLine" => Environment.NewLine,
            _ => "{" + token + "}"
        });
    }

    private static (string Name, string? Format) SplitToken(string token)
    {
        var separator = token.IndexOf(':');
        return separator < 0
            ? (token.Trim(), null)
            : (token[..separator].Trim(), token[(separator + 1)..]);
    }

    private static string FormatTimestamp(DateTimeOffset timestamp, string? format, string fallbackTimestampFormat)
    {
        try
        {
            return timestamp.ToString(
                string.IsNullOrWhiteSpace(format) ? fallbackTimestampFormat : format,
                CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return timestamp.ToString(fallbackTimestampFormat, CultureInfo.InvariantCulture);
        }
    }

    private static string FormatLevel(LogLevel level, string? format)
    {
        return format switch
        {
            "zh" => level.Description(),
            "u3" => ToUpperInvariant(level, 3),
            "u4" => ToUpperInvariant(level, 4),
            _ => level.ToString()
        };
    }

    private static string ToUpperInvariant(LogLevel level, int length)
    {
        var text = level switch
        {
            LogLevel.Information => "Info",
            LogLevel.Warning => "Warn",
            LogLevel.Critical => "Critical",
            _ => level.ToString()
        };

        return text.Length <= length
            ? text.ToUpperInvariant()
            : text[..length].ToUpperInvariant();
    }

    private static string FormatEventId(EventId eventId)
    {
        if (eventId.Id == 0 && string.IsNullOrWhiteSpace(eventId.Name)) return string.Empty;
        return string.IsNullOrWhiteSpace(eventId.Name)
            ? eventId.Id.ToString(CultureInfo.InvariantCulture)
            : $"{eventId.Id}:{eventId.Name}";
    }

    private static void AddPart(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{name}={value}");
    }
}
