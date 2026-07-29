using Avalonia.Controls.Documents;
using Avalonia.Media;
using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeWF.Log.Avalonia;

internal sealed class LogViewInlineRenderer
{
    private const string LevelOpeningDelimiters = "[【(（<《";
    private const string LevelClosingDelimiters = "]】)）>》:：";

    private static readonly SolidColorBrush TimestampBrush = new(Color.Parse("#8C8C8C"));
    private static readonly SolidColorBrush ContentBrush = new(Color.Parse("#262626"));
    private static readonly SolidColorBrush DebugBrush = new(Color.Parse("#1890FF"));
    private static readonly SolidColorBrush InfoBrush = new(Color.Parse("#52C41A"));
    private static readonly SolidColorBrush WarnBrush = new(Color.Parse("#FAAD14"));
    private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#FF4D4F"));
    private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#262626"));

    private readonly Queue<int> _inlineCounts = new();

    internal int RenderedEntryCount => _inlineCounts.Count;

    internal void Rebuild(
        InlineCollection inlines,
        IReadOnlyList<CodeWFLogEvent> entries,
        string template,
        string timestampFormat)
    {
        Clear(inlines);
        AppendUnrenderedEntries(inlines, entries, template, timestampFormat);
    }

    internal void Synchronize(
        InlineCollection inlines,
        IReadOnlyList<CodeWFLogEvent> entries,
        int removedEntryCount,
        string template,
        string timestampFormat)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(removedEntryCount);
        RemoveRenderedEntries(inlines, removedEntryCount);
        if (_inlineCounts.Count > entries.Count)
        {
            Rebuild(inlines, entries, template, timestampFormat);
            return;
        }

        AppendUnrenderedEntries(inlines, entries, template, timestampFormat);
    }

    internal void Clear(InlineCollection inlines)
    {
        _inlineCounts.Clear();
        inlines.Clear();
    }

    private void RemoveRenderedEntries(InlineCollection inlines, int removedEntryCount)
    {
        var renderedEntriesToRemove = Math.Min(removedEntryCount, _inlineCounts.Count);
        var inlineCount = 0;
        for (var index = 0; index < renderedEntriesToRemove; index++)
            inlineCount += _inlineCounts.Dequeue();

        if (inlineCount > 0) inlines.RemoveRange(0, inlineCount);
    }

    private void AppendUnrenderedEntries(
        InlineCollection inlines,
        IReadOnlyList<CodeWFLogEvent> entries,
        string template,
        string timestampFormat)
    {
        if (_inlineCounts.Count >= entries.Count) return;

        var pendingInlines = new List<Inline>();
        var pendingCounts = new List<int>(entries.Count - _inlineCounts.Count);
        for (var index = _inlineCounts.Count; index < entries.Count; index++)
        {
            var initialCount = pendingInlines.Count;
            AddEntryInlines(pendingInlines, entries[index], template, timestampFormat);
            pendingCounts.Add(pendingInlines.Count - initialCount);
        }

        if (pendingInlines.Count > 0) inlines.AddRange(pendingInlines);
        foreach (var count in pendingCounts) _inlineCounts.Enqueue(count);
    }

    private static void AddEntryInlines(
        ICollection<Inline> inlines,
        CodeWFLogEvent entry,
        string template,
        string timestampFormat)
    {
        var segments = LogTemplateFormatter.FormatSegments(entry, template, timestampFormat);
        for (var index = 0; index < segments.Count; index++)
            AddSegmentRun(inlines, segments, index, entry.Level);
    }

    private static void AddSegmentRun(
        ICollection<Inline> inlines,
        IReadOnlyList<LogTemplateSegment> segments,
        int index,
        LogLevel level)
    {
        var segment = segments[index];
        if (segment.Text.Length == 0) return;

        if (segment.TokenName == "Timestamp")
        {
            inlines.Add(CreateRun(segment.Text, TimestampBrush));
            return;
        }

        if (segment.TokenName == "Level" || IsLevelDecoration(segments, index))
        {
            inlines.Add(CreateRun(
                segment.Text,
                GetLevelForeground(level),
                level == LogLevel.Critical ? FontWeight.Bold : FontWeight.Normal));
            return;
        }

        inlines.Add(CreateRun(segment.Text, ContentBrush));
    }

    private static bool IsLevelDecoration(IReadOnlyList<LogTemplateSegment> segments, int index)
    {
        var text = segments[index].Text.Trim();
        if (text.Length == 0) return false;

        var followsLevel = index > 0 && segments[index - 1].TokenName == "Level";
        var precedesLevel = index + 1 < segments.Count && segments[index + 1].TokenName == "Level";
        return followsLevel && text.All(LevelClosingDelimiters.Contains) ||
               precedesLevel && text.All(LevelOpeningDelimiters.Contains);
    }

    private static Run CreateRun(string text, IBrush foreground, FontWeight? fontWeight = null) =>
        new(text)
        {
            Foreground = foreground,
            FontWeight = fontWeight ?? FontWeight.Normal,
            BaselineAlignment = BaselineAlignment.Center
        };

    private static IBrush GetLevelForeground(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => DebugBrush,
        LogLevel.Information => InfoBrush,
        LogLevel.Warning => WarnBrush,
        LogLevel.Error or LogLevel.Critical => ErrorBrush,
        _ => DefaultBrush
    };
}
