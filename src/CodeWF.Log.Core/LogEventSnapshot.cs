namespace CodeWF.Log.Core;

internal static class LogEventSnapshot
{
    private const int MaxEventCharacters = 256 * 1024;
    private const int MaxValueNodes = 4_096;
    private const int MaxDepth = 8;
    private const int MaxCollectionCount = 128;
    private const int MaxPropertyCount = 256;
    private const int MaxScopeCount = 128;
    private const int MaxStringLength = 64 * 1024;
    private const string TruncatedValue = "<maximum event size reached>";

    public static CodeWFLogEvent Capture(CodeWFLogEvent source)
    {
        var budget = new CaptureBudget();
        return source with
        {
            LocalizedLevel = budget.TakeNullable(source.LocalizedLevel),
            CategoryName = budget.Take(source.CategoryName),
            MessageTemplate = budget.TakeNullable(source.MessageTemplate),
            Message = budget.Take(source.Message),
            UserMessage = budget.TakeNullable(source.UserMessage),
            Exception = CaptureException(source.Exception, budget),
            Properties = CaptureProperties(source.Properties, budget),
            Scopes = CaptureScopes(source.Scopes, budget),
            TraceId = budget.TakeNullable(source.TraceId),
            SpanId = budget.TakeNullable(source.SpanId),
            ParentId = budget.TakeNullable(source.ParentId),
            TraceState = budget.TakeNullable(source.TraceState),
            ActivityTags = CaptureProperties(source.ActivityTags, budget),
            ActivityBaggage = CaptureProperties(source.ActivityBaggage, budget)
        };
    }

    private static IReadOnlyList<LogScope> CaptureScopes(
        IReadOnlyList<LogScope>? scopes,
        CaptureBudget budget)
    {
        if (scopes is null || scopes.Count == 0) return [];

        var captured = new List<LogScope>(Math.Min(scopes.Count, MaxScopeCount));
        foreach (var scope in scopes)
        {
            if (captured.Count >= MaxScopeCount || !budget.TryUseNode()) break;
            captured.Add(new LogScope(
                budget.TakeNullable(scope.Text),
                CaptureProperties(scope.Properties, budget)));
        }

        return Array.AsReadOnly(captured.ToArray());
    }

    private static IReadOnlyList<LogProperty> CaptureProperties(
        IReadOnlyList<LogProperty>? properties,
        CaptureBudget budget)
    {
        if (properties is null || properties.Count == 0) return [];

        var captured = new List<LogProperty>(Math.Min(properties.Count, MaxPropertyCount));
        foreach (var property in properties)
        {
            if (captured.Count >= MaxPropertyCount || !budget.TryUseNode()) break;
            captured.Add(new LogProperty(
                budget.Take(property.Name),
                CaptureValue(property.Value, budget, 0)));
        }

        return Array.AsReadOnly(captured.ToArray());
    }

    private static LogValue CaptureValue(LogValue? value, CaptureBudget budget, int depth)
    {
        if (!budget.TryUseNode() || value is null) return new ScalarLogValue(TruncatedValue);
        if (depth >= MaxDepth) return new ScalarLogValue(TruncatedValue);

        return value switch
        {
            ScalarLogValue scalar => new ScalarLogValue(CaptureScalar(scalar.Value, budget)),
            SequenceLogValue sequence => new SequenceLogValue(
                CaptureValues(sequence.Values, budget, depth + 1)),
            StructureLogValue structure => new StructureLogValue(
                budget.TakeNullable(structure.TypeName),
                CaptureProperties(structure.Properties, budget)),
            _ => new ScalarLogValue(TruncatedValue)
        };
    }

    private static IReadOnlyList<LogValue> CaptureValues(
        IReadOnlyList<LogValue>? values,
        CaptureBudget budget,
        int depth)
    {
        if (values is null || values.Count == 0) return [];

        var captured = new List<LogValue>(Math.Min(values.Count, MaxCollectionCount));
        foreach (var value in values)
        {
            if (captured.Count >= MaxCollectionCount) break;
            captured.Add(CaptureValue(value, budget, depth));
        }

        return Array.AsReadOnly(captured.ToArray());
    }

    private static object? CaptureScalar(object? value, CaptureBudget budget)
    {
        if (value is null) return null;
        if (value is string text) return budget.Take(text);
        if (value.GetType().IsPrimitive || value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Enum)
            return value;

        try { return budget.Take(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty); }
        catch { return TruncatedValue; }
    }

    private static LogExceptionInfo? CaptureException(LogExceptionInfo? exception, CaptureBudget budget)
    {
        if (exception is null || !budget.TryUseNode()) return null;

        var inner = new List<LogExceptionInfo>();
        if (exception.InnerExceptions is not null)
        {
            foreach (var item in exception.InnerExceptions)
            {
                if (inner.Count >= 32) break;
                var captured = CaptureException(item, budget);
                if (captured is null) break;
                inner.Add(captured);
            }
        }

        return new LogExceptionInfo
        {
            TypeName = budget.Take(exception.TypeName),
            Message = budget.Take(exception.Message),
            Text = budget.Take(exception.Text),
            StackTrace = budget.TakeNullable(exception.StackTrace),
            Source = budget.TakeNullable(exception.Source),
            HResult = exception.HResult,
            InnerExceptions = Array.AsReadOnly(inner.ToArray())
        };
    }

    private sealed class CaptureBudget
    {
        private int _remainingCharacters = MaxEventCharacters;
        private int _usedNodes;

        public bool TryUseNode() => _usedNodes++ < MaxValueNodes;

        public string Take(string? value)
        {
            value ??= string.Empty;
            var length = Math.Min(value.Length, Math.Min(MaxStringLength, Math.Max(_remainingCharacters, 0)));
            _remainingCharacters -= length;
            return length == value.Length ? value : value[..length] + "…";
        }

        public string? TakeNullable(string? value) => value is null ? null : Take(value);
    }
}
