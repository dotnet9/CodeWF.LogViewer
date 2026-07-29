using CodeWF.Log.Core.Extensions;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace CodeWF.Log.Core;

public static class LogTemplateFormatter
{
    private static readonly HashSet<string> SupportedTokens =
    [
        "Timestamp", "Level", "Category", "CategoryName", "EventId", "EventName",
        "Message", "MessageTemplate", "UserMessage", "Properties", "Scopes", "Activity",
        "TraceId", "SpanId", "ParentId", "TraceState", "TraceFlags", "ActivityTags",
        "ActivityBaggage", "Exception", "NewLine"
    ];
    private static readonly ConditionalWeakTable<string, CompiledLogTemplate> CompiledTemplates = new();

    public static string Format(CodeWFLogEvent logEvent, string outputTemplate, string fallbackTimestampFormat)
    {
        var template = GetCompiledTemplate(outputTemplate);
        var builder = new StringBuilder(outputTemplate.Length + logEvent.Message.Length + 32);
        foreach (var part in template.Parts)
        {
            builder.Append(part.TokenName is null
                ? part.Text
                : FormatToken(logEvent, part, fallbackTimestampFormat));
        }

        return builder.ToString();
    }

    public static IReadOnlyList<LogTemplateSegment> FormatSegments(
        CodeWFLogEvent logEvent,
        string outputTemplate,
        string fallbackTimestampFormat)
    {
        var template = GetCompiledTemplate(outputTemplate);
        var segments = new List<LogTemplateSegment>(template.Parts.Length);
        foreach (var part in template.Parts)
        {
            segments.Add(new LogTemplateSegment(
                part.TokenName is null
                    ? part.Text
                    : FormatToken(logEvent, part, fallbackTimestampFormat),
                part.TokenName));
        }

        return segments;
    }

    public static bool TryValidate(string? template, out string? error)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            error = "日志模板不能为空。";
            return false;
        }

        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] == '{')
            {
                if (index + 1 < template.Length && template[index + 1] == '{')
                {
                    index++;
                    continue;
                }

                var end = template.IndexOf('}', index + 1);
                if (end < 0)
                {
                    error = $"模板位置 {index} 缺少右花括号。";
                    return false;
                }

                var rawToken = template[(index + 1)..end];
                if (rawToken.Contains('{'))
                {
                    error = $"模板位置 {index} 包含未转义的左花括号。";
                    return false;
                }

                var (name, format) = SplitToken(rawToken);
                if (!SupportedTokens.Contains(name))
                {
                    error = $"不支持模板占位符 {{{name}}}。";
                    return false;
                }

                if (!TryValidateFormat(name, format, out error)) return false;
                index = end;
                continue;
            }

            if (template[index] != '}') continue;
            if (index + 1 < template.Length && template[index + 1] == '}')
            {
                index++;
                continue;
            }

            error = $"模板位置 {index} 包含未转义的右花括号。";
            return false;
        }

        _ = GetCompiledTemplate(template);
        error = null;
        return true;
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

    internal static CompiledLogTemplate GetCompiledTemplate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return CompiledTemplates.GetValue(template, static value => Compile(value));
    }

    private static string FormatToken(
        CodeWFLogEvent logEvent,
        TemplatePart token,
        string fallbackTimestampFormat)
    {
        return token.TokenName switch
        {
            "Timestamp" => FormatTimestamp(logEvent.Timestamp, token.Format, fallbackTimestampFormat),
            "Level" => FormatLevel(logEvent.Level, token.Format),
            "Category" or "CategoryName" => logEvent.CategoryName,
            "EventId" => FormatEventId(logEvent.EventId),
            "EventName" => logEvent.EventId.Name,
            "Message" => logEvent.Message,
            "MessageTemplate" => logEvent.MessageTemplate,
            "UserMessage" => string.IsNullOrWhiteSpace(logEvent.UserMessage)
                ? logEvent.Message
                : logEvent.UserMessage,
            "Properties" => FormatProperties(logEvent.Properties),
            "Scopes" => FormatScopes(logEvent.Scopes),
            "Activity" => FormatActivity(logEvent),
            "TraceId" => logEvent.TraceId,
            "SpanId" => logEvent.SpanId,
            "ParentId" => logEvent.ParentId,
            "TraceState" => logEvent.TraceState,
            "TraceFlags" => logEvent.TraceFlags.ToString(),
            "ActivityTags" => FormatProperties(logEvent.ActivityTags),
            "ActivityBaggage" => FormatProperties(logEvent.ActivityBaggage),
            "Exception" => logEvent.Exception?.ToString(),
            "NewLine" => Environment.NewLine,
            _ => "{" + token.Text + "}"
        } ?? string.Empty;
    }

    private static CompiledLogTemplate Compile(string template)
    {
        var parts = new List<TemplatePart>();
        var literal = new StringBuilder();
        for (var index = 0; index < template.Length; index++)
        {
            var current = template[index];
            if (current == '{')
            {
                if (index + 1 < template.Length && template[index + 1] == '{')
                {
                    literal.Append('{');
                    index++;
                    continue;
                }

                var end = template.IndexOf('}', index + 1);
                if (end > index)
                {
                    AddLiteralPart(parts, literal);
                    var rawToken = template[(index + 1)..end];
                    var (name, format) = SplitToken(rawToken);
                    parts.Add(new TemplatePart(rawToken, name, format));
                    index = end;
                    continue;
                }
            }

            if (current == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                literal.Append('}');
                index++;
                continue;
            }

            literal.Append(current);
        }

        AddLiteralPart(parts, literal);
        return new CompiledLogTemplate(parts.ToArray());
    }

    private static void AddLiteralPart(List<TemplatePart> parts, StringBuilder literal)
    {
        if (literal.Length == 0) return;
        parts.Add(new TemplatePart(literal.ToString(), null, null));
        literal.Clear();
    }

    internal sealed class CompiledLogTemplate(TemplatePart[] parts)
    {
        internal TemplatePart[] Parts { get; } = parts;
    }

    internal readonly record struct TemplatePart(string Text, string? TokenName, string? Format);

    private static (string Name, string? Format) SplitToken(string token)
    {
        var separator = token.IndexOf(':');
        return separator < 0
            ? (token.Trim(), null)
            : (token[..separator].Trim(), token[(separator + 1)..]);
    }

    private static bool TryValidateFormat(string name, string? format, out string? error)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            error = null;
            return true;
        }

        if (name == "Timestamp")
        {
            try { _ = DateTimeOffset.Now.ToString(format, CultureInfo.InvariantCulture); }
            catch (FormatException)
            {
                error = $"Timestamp 格式 '{format}' 无效。";
                return false;
            }
            error = null;
            return true;
        }

        if (name == "Level" && format is "zh" or "u3" or "u4")
        {
            error = null;
            return true;
        }

        error = $"占位符 {{{name}}} 不支持格式 '{format}'。";
        return false;
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
