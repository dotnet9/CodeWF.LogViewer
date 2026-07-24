using System.Collections;

namespace CodeWF.Log.Extensions.Logging;

internal interface ICodeWFUserLogState
{
    string UserMessage { get; }
}

internal sealed class CodeWFUserLogState : IReadOnlyList<KeyValuePair<string, object?>>, ICodeWFUserLogState
{
    private readonly string _messageTemplate;
    private readonly object?[] _args;
    private readonly KeyValuePair<string, object?>[] _properties;

    public CodeWFUserLogState(string userMessage, string messageTemplate, object?[] args)
    {
        UserMessage = userMessage;
        _messageTemplate = messageTemplate;
        _args = args;
        var names = MessageTemplateParser.GetPropertyNames(messageTemplate);
        var properties = new List<KeyValuePair<string, object?>>(args.Length + 1);
        for (var index = 0; index < args.Length; index++)
        {
            var name = index < names.Count ? names[index] : $"Arg{index}";
            properties.Add(new KeyValuePair<string, object?>(name, args[index]));
        }
        properties.Add(new KeyValuePair<string, object?>(CodeWFLogPropertyNames.OriginalFormat, messageTemplate));
        _properties = properties.ToArray();
    }

    public string UserMessage { get; }
    public int Count => _properties.Length;
    public KeyValuePair<string, object?> this[int index] => _properties[index];
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        ((IEnumerable<KeyValuePair<string, object?>>)_properties).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => MessageTemplateParser.Format(_messageTemplate, _args);
}
