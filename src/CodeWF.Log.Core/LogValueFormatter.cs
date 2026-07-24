using System.Collections;
using System.Globalization;
using System.Text;

namespace CodeWF.Log.Core;

internal static class LogValueFormatter
{
    private const int MaxDepth = 8;
    private const int MaxCollectionCount = 64;
    private const int MaxPropertyCount = 64;
    private const int MaxStringLength = 32 * 1024;

    public static LogValue Capture(object? value) =>
        Capture(value, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static LogValue Capture(object? value, int depth, HashSet<object> ancestors)
    {
        if (value is null)
            return new ScalarLogValue(null);

        if (value is string text)
            return new ScalarLogValue(Truncate(text));

        if (
            value.GetType().IsPrimitive ||
            value is decimal ||
            value is DateTime ||
            value is DateTimeOffset ||
            value is TimeSpan ||
            value is Guid)
            return new ScalarLogValue(value);

        if (depth >= MaxDepth)
            return new ScalarLogValue("<maximum depth reached>");

        var trackReference = !value.GetType().IsValueType;
        if (trackReference && !ancestors.Add(value))
            return new ScalarLogValue("<cycle>");

        try
        {
        if (value is IEnumerable<KeyValuePair<string, object?>> properties)
            return new StructureLogValue(null, CaptureProperties(properties, depth + 1, ancestors));

        if (value is IEnumerable sequence)
        {
            var values = new List<LogValue>();
            foreach (var item in sequence)
            {
                values.Add(Capture(item, depth + 1, ancestors));
                if (values.Count >= MaxCollectionCount) break;
            }

            return new SequenceLogValue(values);
        }

            return new ScalarLogValue(Truncate(SafeToString(value)));
        }
        finally
        {
            if (trackReference) ancestors.Remove(value);
        }
    }

    public static IReadOnlyList<LogProperty> CaptureProperties(IEnumerable<KeyValuePair<string, object?>> properties)
        => CaptureProperties(properties, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static IReadOnlyList<LogProperty> CaptureProperties(
        IEnumerable<KeyValuePair<string, object?>> properties,
        int depth,
        HashSet<object> ancestors)
    {
        var captured = new List<LogProperty>();
        foreach (var property in properties)
        {
            if (property.Key == "{OriginalFormat}") continue;
            captured.Add(new LogProperty(Truncate(property.Key), Capture(property.Value, depth, ancestors)));
            if (captured.Count >= MaxPropertyCount) break;
        }

        return captured;
    }

    public static string ToDisplayString(LogValue value)
    {
        return value switch
        {
            ScalarLogValue scalar => Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            SequenceLogValue sequence => "[" + string.Join(", ", sequence.Values.Select(ToDisplayString)) + "]",
            StructureLogValue structure => FormatStructure(structure),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatStructure(StructureLogValue structure)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(structure.TypeName))
            builder.Append(structure.TypeName).Append(' ');

        builder.Append('{');
        for (var index = 0; index < structure.Properties.Count; index++)
        {
            if (index > 0) builder.Append(", ");
            var property = structure.Properties[index];
            builder.Append(property.Name).Append(" = ").Append(ToDisplayString(property.Value));
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string SafeToString(object value)
    {
        try { return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty; }
        catch { return value.GetType().FullName ?? value.GetType().Name; }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxStringLength ? value : value[..MaxStringLength] + "…";
}
