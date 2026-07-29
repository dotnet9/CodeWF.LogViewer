using Avalonia.Controls.Documents;
using CodeWF.Log.Avalonia;
using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeWF.Log.Avalonia.Tests;

public sealed class LogViewInlineRendererTests
{
    [Fact]
    public void Synchronize_AppendsAndTrimsWithoutRecreatingRetainedEntries()
    {
        var renderer = new LogViewInlineRenderer();
        var inlines = new InlineCollection();
        var entries = new List<CodeWFLogEvent>
        {
            CreateEvent(1, "first"),
            CreateEvent(2, "second")
        };

        renderer.Rebuild(inlines, entries, LineTemplateController.DefaultTemplate, "O");
        var originalInlines = inlines.ToArray();

        entries.Add(CreateEvent(3, "third"));
        renderer.Synchronize(inlines, entries, 0, LineTemplateController.DefaultTemplate, "O");

        Assert.Equal(3, renderer.RenderedEntryCount);
        for (var index = 0; index < originalInlines.Length; index++)
            Assert.Same(originalInlines[index], inlines[index]);

        entries.RemoveAt(0);
        renderer.Synchronize(inlines, entries, 1, LineTemplateController.DefaultTemplate, "O");

        Assert.Equal(2, renderer.RenderedEntryCount);
        Assert.DoesNotContain(originalInlines[0], inlines);
        Assert.Contains(originalInlines[^1], inlines);
        Assert.DoesNotContain("first", inlines.Text);
        Assert.Contains("second", inlines.Text);
        Assert.Contains("third", inlines.Text);
    }

    [Fact]
    public void Rebuild_ReplacesExistingInlinesForTemplateChanges()
    {
        var renderer = new LogViewInlineRenderer();
        var inlines = new InlineCollection();
        var entries = new[] { CreateEvent(1, "first") };

        renderer.Rebuild(inlines, entries, LineTemplateController.DefaultTemplate, "O");
        var originalInlines = inlines.ToArray();
        renderer.Rebuild(inlines, entries, "{Level:u3}|{UserMessage}{NewLine}", "O");

        Assert.Equal(1, renderer.RenderedEntryCount);
        Assert.DoesNotContain(inlines, inline => originalInlines.Contains(inline));
        Assert.Equal($"INF|first{Environment.NewLine}", inlines.Text);
    }

    [Fact]
    public void Synchronize_FullBufferRetainsAllUnchangedInlineInstances()
    {
        var renderer = new LogViewInlineRenderer();
        var inlines = new InlineCollection();
        var entries = Enumerable.Range(1, 1_000)
            .Select(index => CreateEvent(index, $"message-{index}"))
            .ToList();

        renderer.Rebuild(inlines, entries, LineTemplateController.DefaultTemplate, "O");
        var originalInlines = inlines.ToArray();
        Assert.Equal(6_000, originalInlines.Length);

        entries.RemoveAt(0);
        entries.Add(CreateEvent(1_001, "message-1001"));
        renderer.Synchronize(inlines, entries, 1, LineTemplateController.DefaultTemplate, "O");

        Assert.Equal(1_000, renderer.RenderedEntryCount);
        Assert.Equal(6_000, inlines.Count);
        for (var index = 0; index < 5_994; index++)
            Assert.Same(originalInlines[index + 6], inlines[index]);
    }

    private static CodeWFLogEvent CreateEvent(long sequence, string message) => new()
    {
        Sequence = sequence,
        Timestamp = new DateTimeOffset(2026, 7, 29, 12, 34, 56, TimeSpan.FromHours(8)),
        Level = LogLevel.Information,
        CategoryName = "Tests",
        Message = message,
        UserMessage = message
    };
}
