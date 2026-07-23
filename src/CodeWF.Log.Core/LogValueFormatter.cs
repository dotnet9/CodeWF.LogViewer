using System.Collections;
using System.Globalization;
using System.Text;

namespace CodeWF.Log.Core;

internal static class LogValueFormatter
{
    public static LogValue Capture(object? value)
    {
        if (value is null ||
            value is string ||
            value.GetType().IsPrimitive ||
            value is decimal ||
            value is DateTime ||
            value is DateTimeOffset ||
            value is TimeSpan ||
            value is Guid)
            return new ScalarLogValue(value);

        if (value is IEnumerable<KeyValuePair<string, object?>> properties)
            return new StructureLogValue(null, CaptureProperties(properties));

        if (value is IEnumerable sequence)
        {
            var values = new List<LogValue>();
            foreach (var item in sequence)
            {
                values.Add(Capture(item));
                if (values.Count >= 64) break;
            }

            return new SequenceLogValue(values);
        }

        return new ScalarLogValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString());
    }

    public static IReadOnlyList<LogProperty> CaptureProperties(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        var captured = new List<LogProperty>();
        foreach (var property in properties)
        {
            if (property.Key == "{OriginalFormat}") continue;
            captured.Add(new LogProperty(property.Key, Capture(property.Value)));
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
}
