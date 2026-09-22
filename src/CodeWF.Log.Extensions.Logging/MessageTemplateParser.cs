using System.Globalization;
using System.Text;

namespace CodeWF.Log.Extensions.Logging;

internal static class MessageTemplateParser
{
    public static IReadOnlyList<string> GetPropertyNames(string messageTemplate)
    {
        var names = new List<string>();
        foreach (var placeholder in EnumeratePlaceholders(messageTemplate))
        {
            var name = NormalizePropertyName(placeholder.Name);
            names.Add(string.IsNullOrWhiteSpace(name) ? $"Arg{names.Count}" : name);
        }

        return names;
    }

    public static string Format(string messageTemplate, IReadOnlyList<object?> args)
    {
        if (args.Count == 0 || string.IsNullOrEmpty(messageTemplate)) return messageTemplate;

        var builder = new StringBuilder(messageTemplate.Length + args.Count * 8);
        var index = 0;
        var argumentIndex = 0;
        foreach (var placeholder in EnumeratePlaceholders(messageTemplate))
        {
            builder.Append(messageTemplate, index, placeholder.Start - index);
            if (argumentIndex < args.Count)
                builder.Append(FormatValue(args[argumentIndex], placeholder.Alignment, placeholder.Format));
            else
                builder.Append(messageTemplate, placeholder.Start, placeholder.Length);

            argumentIndex++;
            index = placeholder.Start + placeholder.Length;
        }

        builder.Append(messageTemplate, index, messageTemplate.Length - index);
        return builder.ToString().Replace("{{", "{", StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal);
    }

    private static IEnumerable<Placeholder> EnumeratePlaceholders(string messageTemplate)
    {
        for (var index = 0; index < messageTemplate.Length; index++)
        {
            if (messageTemplate[index] != '{') continue;
            if (index + 1 < messageTemplate.Length && messageTemplate[index + 1] == '{')
            {
                index++;
                continue;
            }

            var end = FindPlaceholderEnd(messageTemplate, index + 1);
            if (end < 0) continue;

            var content = messageTemplate[(index + 1)..end];
            var (name, alignment, format) = SplitPlaceholder(content);
            yield return new Placeholder(index, end - index + 1, name, alignment, format);
            index = end;
        }
    }

    private static int FindPlaceholderEnd(string messageTemplate, int start)
    {
        for (var index = start; index < messageTemplate.Length; index++)
        {
            if (messageTemplate[index] == '}')
            {
                if (index + 1 < messageTemplate.Length && messageTemplate[index + 1] == '}')
                {
                    index++;
                    continue;
                }

                return index;
            }
        }

        return -1;
    }

    private static (string Name, int? Alignment, string? Format) SplitPlaceholder(string content)
    {
        var colon = content.IndexOf(':');
        var nameAndAlignment = colon < 0 ? content : content[..colon];
        var format = colon < 0 ? null : content[(colon + 1)..];
        var comma = nameAndAlignment.IndexOf(',');
        if (comma < 0)
            return (nameAndAlignment.Trim(), null, string.IsNullOrWhiteSpace(format) ? null : format);

        var name = nameAndAlignment[..comma].Trim();
        var alignmentText = nameAndAlignment[(comma + 1)..].Trim();
        var alignment = int.TryParse(alignmentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (int?)null;
        return (name, alignment, string.IsNullOrWhiteSpace(format) ? null : format);
    }

    private static string NormalizePropertyName(string name)
    {
        if (name.Length > 0 && (name[0] == '@' || name[0] == '$'))
            return name[1..];

        return name;
    }

    private static string FormatValue(object? value, int? alignment, string? format)
    {
        var text = value switch
        {
            null => string.Empty,
            IFormattable formattable when !string.IsNullOrWhiteSpace(format) =>
                formattable.ToString(format, CultureInfo.CurrentCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? value.ToString() ?? string.Empty
        };

        return alignment switch
        {
            > 0 => text.PadLeft(alignment.Value),
            < 0 => text.PadRight(-alignment.Value),
            _ => text
        };
    }

    private readonly record struct Placeholder(
        int Start,
        int Length,
        string Name,
        int? Alignment,
        string? Format);
}
