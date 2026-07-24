namespace CodeWF.Log.Core;

public interface ILineTemplateController
{
    string Current { get; }
    event EventHandler? Changed;
    bool TryUpdate(string template, out string? error);
}

public sealed class LineTemplateController : ILineTemplateController
{
    public const string DefaultTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:zh}] {UserMessage}{NewLine}";

    private string _current;

    public LineTemplateController(string? template = null)
    {
        _current = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        if (!LogTemplateFormatter.TryValidate(_current, out var error))
            throw new ArgumentException(error, nameof(template));
    }

    public string Current => Volatile.Read(ref _current);

    public event EventHandler? Changed;

    public bool TryUpdate(string template, out string? error)
    {
        if (!LogTemplateFormatter.TryValidate(template, out error)) return false;
        if (string.Equals(Current, template, StringComparison.Ordinal)) return true;
        Volatile.Write(ref _current, template);
        if (Changed is { } changed)
        {
            foreach (EventHandler handler in changed.GetInvocationList())
            {
                try { handler(this, EventArgs.Empty); }
                catch (Exception ex) { LoggerSelfDiagnostics.Report("通知 LineTemplate 更新失败。", ex); }
            }
        }
        return true;
    }
}
