namespace CodeWF.Log.Core;

/// <summary>
/// A formatted output-template segment and the token that produced it.
/// </summary>
public readonly record struct LogTemplateSegment(string Text, string? TokenName);
