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
                builder.Append(FormatValue(args[argumentIndex], placeholder.Format));
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
            var (name, format) = SplitPlaceholder(content);
            yield return new Placeholder(index, end - index + 1, name, format);
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

    private static (string Name, string? Format) SplitPlaceholder(string content)
    {
        var separator = content.IndexOfAny([',', ':']);
        if (separator < 0) return (content.Trim(), null);

        var name = content[..separator].Trim();
        var format = content[separator] == ':' ? content[(separator + 1)..] : null;
        return (name, string.IsNullOrWhiteSpace(format) ? null : format);
    }

    private static string NormalizePropertyName(string name)
    {
        if (name.Length > 0 && (name[0] == '@' || name[0] == '$'))
            return name[1..];

        return name;
    }

    private static string FormatValue(object? value, string? format)
    {
        if (value is null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(format) && value is IFormattable formattable)
            return formattable.ToString(format, CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty;
    }

    private readonly record struct Placeholder(int Start, int Length, string Name, string? Format);
}
