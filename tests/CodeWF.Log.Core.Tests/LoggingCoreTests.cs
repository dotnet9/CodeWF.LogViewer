using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;

namespace CodeWF.Log.Core.Tests;

public sealed class LoggingCoreTests
{
    [Fact]
    public void Formatter_UsesUserMessageAndFallsBackToMessage()
    {
        var logEvent = CreateEvent() with { Message = "diagnostic", UserMessage = "friendly" };
        Assert.Equal("diagnostic|friendly", LogTemplateFormatter.Format(logEvent, "{Message}|{UserMessage}", "O"));
        Assert.Equal("diagnostic", LogTemplateFormatter.Format(logEvent with { UserMessage = "  " }, "{UserMessage}", "O"));
    }

    [Fact]
    public void Formatter_FormatsCompleteEventFields()
    {
        var logEvent = CreateEvent() with
        {
            CategoryName = "Demo.Category",
            EventId = new EventId(42, "Answer"),
            MessageTemplate = "Value {Value}",
            Properties = [new LogProperty("Value", new ScalarLogValue(42))],
            Scopes = [new LogScope("scope", [new LogProperty("Device", new ScalarLogValue("PLC-01"))])],
            TraceId = "trace",
            SpanId = "span",
            Exception = LogExceptionInfo.Capture(new InvalidOperationException("broken"))
        };

        var text = LogTemplateFormatter.Format(
            logEvent,
            "{Category}|{EventId}|{MessageTemplate}|{Properties}|{Scopes}|{TraceId}|{SpanId}|{Exception}",
            "O");

        Assert.Contains("Demo.Category|42:Answer|Value {Value}|Value=42", text);
        Assert.Contains("scope", text);
        Assert.Contains("trace|span", text);
        Assert.Contains("InvalidOperationException", text);
    }

    [Fact]
    public void LineTemplateController_RejectsInvalidUpdateAtomically()
    {
        var controller = new LineTemplateController("{Message}");
        Assert.False(controller.TryUpdate("{Unknown}", out var error));
        Assert.Contains("Unknown", error);
        Assert.Equal("{Message}", controller.Current);
        Assert.True(controller.TryUpdate("{UserMessage}", out error));
        Assert.Null(error);
        Assert.Equal("{UserMessage}", controller.Current);
        Assert.False(controller.TryUpdate("{Message}}", out error));
        Assert.Contains("右花括号", error);
        Assert.False(controller.TryUpdate("{Timestamp:not-a-format-%}", out error));
    }

    [Fact]
    public void FileOutputTemplateController_SupportsAtomicRuntimeUpdates()
    {
        var controller = new FileOutputTemplateController("{Message}{NewLine}");
        Assert.True(controller.TryUpdate("{Level:u3}|{Message}{NewLine}", out var error));
        Assert.Null(error);
        Assert.Equal("{Level:u3}|{Message}{NewLine}", controller.Current);
        Assert.False(controller.TryUpdate("{Unknown}", out error));
        Assert.Equal("{Level:u3}|{Message}{NewLine}", controller.Current);
        Assert.False(controller.TryUpdate(string.Empty, out error));
        Assert.Equal("{Level:u3}|{Message}{NewLine}", controller.Current);
        Assert.True(controller.TryUpdate(null, out error));
        Assert.Null(controller.Current);
    }

    [Fact]
    public void ValueCapture_IsBoundedAndDetectsCycles()
    {
        var list = new List<object?>();
        list.Add(list);
        var captured = Assert.IsType<SequenceLogValue>(LogValueFormatter.Capture(list));
        Assert.Equal("<cycle>", Assert.IsType<ScalarLogValue>(captured.Values[0]).Value);

        var longText = new string('x', 40_000);
        var scalar = Assert.IsType<ScalarLogValue>(LogValueFormatter.Capture(longText));
        Assert.True(Assert.IsType<string>(scalar.Value).Length < longText.Length);
    }

    [Fact]
    public void ExceptionSnapshot_IsBoundedAndDoesNotRetainException()
    {
        Exception exception = new InvalidOperationException(new string('x', 40_000));
        for (var index = 0; index < 12; index++) exception = new Exception($"outer-{index}", exception);
        var snapshot = LogExceptionInfo.Capture(exception)!;
        Assert.IsType<LogExceptionInfo>(snapshot);
        Assert.True(snapshot.Text.Length <= 32 * 1024 + 1);
        Assert.True(CountDepth(snapshot) <= 9);
        Assert.DoesNotContain(typeof(Exception), snapshot.GetType().GetProperties().Select(property => property.PropertyType));
    }

    [Fact]
    public void HealthSnapshotReportsTotalAndPerLevelDrops()
    {
        var health = new CodeWFLogHealth();
        health.RecordDropped(LogLevel.Debug);
        health.RecordDropped(LogLevel.Debug);
        health.RecordDropped(LogLevel.Error);
        var snapshot = health.GetSnapshot();
        Assert.Equal(3, snapshot.DroppedCount);
        Assert.Equal(2, snapshot.DroppedByLevel[LogLevel.Debug]);
        Assert.Equal(1, snapshot.DroppedByLevel[LogLevel.Error]);
    }

    [Fact]
    public async Task EventFeed_ReplaysThenDeliversLiveEventsInOrder()
    {
        var feed = new LogEventFeed(10, new LineTemplateController());
        feed.Publish(CreateEvent(1));
        feed.Publish(CreateEvent(2));
        var received = new List<long>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = feed.Subscribe(logEvent =>
        {
            lock (received)
            {
                received.Add(logEvent.Sequence);
                if (received.Count == 4) completed.TrySetResult();
            }
        });
        feed.Publish(CreateEvent(3));
        feed.Publish(CreateEvent(4));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([1L, 2L, 3L, 4L], received);
    }

    [Fact]
    public void EventFeed_SlowSubscriberDoesNotBlockPublisher()
    {
        var feed = new LogEventFeed(10, new LineTemplateController());
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var subscription = feed.Subscribe(_ => { entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); }, replayRecent: false);
        var stopwatch = Stopwatch.StartNew();
        feed.Publish(CreateEvent(1));
        stopwatch.Stop();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        release.Set();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task FileSink_RollsBySizeAndFlushesThroughHostBarrier()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodeWF.Log.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var host = new LoggerHost(new LoggerOptions
            {
                MinimumLevel = LogLevel.Trace,
                EnableConsole = false,
                EnableEventFeed = false,
                QueueFullMode = LogQueueFullMode.Wait,
                File = new FileLogOptions
                {
                    DirectoryPath = directory,
                    MaxFileSizeBytes = 120,
                    BatchSize = 100,
                    FlushInterval = TimeSpan.FromMinutes(1),
                    OutputTemplate = "{Message}{NewLine}"
                }
            });
            for (var index = 0; index < 10; index++)
                host.Write(CreateEvent() with { Message = $"{index}:{new string('x', 50)}" });
            await host.FlushAsync();
            await host.DisposeAsync();
            Assert.True(Directory.GetFiles(directory, "*.log").Length >= 5);
            Assert.Contains("9:", string.Concat(Directory.GetFiles(directory, "*.log").Select(File.ReadAllText)));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileSink_RemovesFilesOlderThanRetentionButKeepsRecentFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodeWF.Log.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var oldFile = Path.Combine(directory, "Log_2020_01_01.log");
        var recentFile = Path.Combine(directory, $"Log_{DateTime.Now:yyyy_MM_dd}.log");
        await File.WriteAllTextAsync(oldFile, "old");
        await File.WriteAllTextAsync(recentFile, "recent");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-31));
        try
        {
            await using var sink = new FileLogSink(
                new FileLogOptions { DirectoryPath = directory, RetentionDays = 30 },
                new FileOutputTemplateController());
            Assert.False(File.Exists(oldFile));
            Assert.True(File.Exists(recentFile));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileSink_UsesUpdatedOutputTemplateForSubsequentEvents()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodeWF.Log.Tests", Guid.NewGuid().ToString("N"));
        var controller = new FileOutputTemplateController("{Message}{NewLine}");
        try
        {
            await using (var sink = new FileLogSink(
                new FileLogOptions { DirectoryPath = directory, BatchSize = 1 },
                controller))
            {
                await sink.WriteAsync(CreateEvent() with { Message = "before" }, CancellationToken.None);
                Assert.True(controller.TryUpdate("{Level:u3}|{Message}{NewLine}", out var error), error);
                await sink.WriteAsync(CreateEvent() with { Message = "after" }, CancellationToken.None);
            }

            var text = string.Concat(Directory.GetFiles(directory, "*.log").Select(File.ReadAllText));
            Assert.Contains($"before{Environment.NewLine}", text);
            Assert.Contains($"INF|after{Environment.NewLine}", text);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static CodeWFLogEvent CreateEvent(long sequence = 0) => new()
    {
        Sequence = sequence,
        Timestamp = DateTimeOffset.Now,
        Level = LogLevel.Information,
        CategoryName = "Tests",
        Message = "message"
    };

    private static int CountDepth(LogExceptionInfo exception) =>
        exception.InnerExceptions.Count == 0 ? 1 : 1 + exception.InnerExceptions.Max(CountDepth);
}
