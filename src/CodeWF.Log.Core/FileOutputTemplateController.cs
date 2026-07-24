namespace CodeWF.Log.Core;

public interface IFileOutputTemplateController
{
    string? Current { get; }
    bool TryUpdate(string? template, out string? error);
}

public sealed class FileOutputTemplateController : IFileOutputTemplateController
{
    private string? _current;

    public FileOutputTemplateController(string? template = null)
    {
        if (template is not null && !LogTemplateFormatter.TryValidate(template, out var error))
            throw new ArgumentException(error, nameof(template));
        _current = template;
    }

    public string? Current => Volatile.Read(ref _current);

    public bool TryUpdate(string? template, out string? error)
    {
        if (template is not null && !LogTemplateFormatter.TryValidate(template, out error)) return false;
        Volatile.Write(ref _current, template);
        error = null;
        return true;
    }
}
