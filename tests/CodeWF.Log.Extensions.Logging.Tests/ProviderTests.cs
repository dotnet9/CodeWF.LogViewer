using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;

namespace CodeWF.Log.Extensions.Logging.Tests;

public sealed class ProviderTests
{
    [Fact]
    public async Task StandardAndUserLogsShareOneCompleteEventModel()
    {
        using var services = BuildServices();
        var logger = services.GetRequiredService<ILogger<ProviderTests>>();
        var feed = services.GetRequiredService<LogEventFeed>();
        logger.LogWarning(new EventId(10, "Standard"), "Device {DeviceId} is slow", "PLC-01");
        logger.LogUserError(
            new InvalidOperationException("connection reset"),
            "设备 PLC-02 连接失败。",
            "Device {DeviceId} connection failed",
            "PLC-02");

        var events = await WaitForEventsAsync(feed, 2);
        Assert.Null(events[0].UserMessage);
        Assert.Equal("Device PLC-01 is slow", events[0].Message);
        Assert.Equal("设备 PLC-02 连接失败。", events[1].UserMessage);
        Assert.Equal("Device PLC-02 connection failed", events[1].Message);
        Assert.NotNull(events[1].Exception);
    }

    [Fact]
    public async Task CapturesScopesActivityAndLoggerMessageState()
    {
        using var services = BuildServices(options =>
        {
            options.Capture.ActivityTags = true;
            options.Capture.ActivityBaggage = true;
        });
        var logger = services.GetRequiredService<ILogger<ProviderTests>>();
        var feed = services.GetRequiredService<LogEventFeed>();
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).AddTag("tag", "value").AddBaggage("bag", "value").Start();
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["Device"] = "PLC-03" });
        var callback = LoggerMessage.Define<int>(LogLevel.Information, new EventId(20, "GeneratedShape"), "Sample {Value}");
        callback(logger, 42, null);

        var logEvent = (await WaitForEventsAsync(feed, 1))[0];
        Assert.Equal("Sample {Value}", logEvent.MessageTemplate);
        Assert.Contains(logEvent.Properties, property => property.Name == "Value");
        Assert.Contains(logEvent.Scopes.SelectMany(item => item.Properties), property => property.Name == "Device");
        Assert.Equal(Activity.Current?.TraceId.ToString(), logEvent.TraceId);
        Assert.Contains(logEvent.ActivityTags, property => property.Name == "tag");
        Assert.Contains(logEvent.ActivityBaggage, property => property.Name == "bag");
    }

    [Fact]
    public async Task UserMessageIsNotExposedToOtherProvidersAsStateProperty()
    {
        var observer = new StateObserverProvider();
        using var services = new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(observer);
                builder.AddCodeWF(options =>
                {
                    options.BridgeStaticLogger = false;
                    options.File.Enabled = false;
                    options.Console.Enabled = false;
                });
            })
            .BuildServiceProvider();
        var logger = services.GetRequiredService<ILogger<ProviderTests>>();
        logger.LogUserWarning("用户可见内容", "Diagnostic {Value}", 42);
        var observation = await observer.Observation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Diagnostic 42", observation.Message);
        Assert.DoesNotContain(observation.Properties, property =>
            property.Key.Contains("UserMessage", StringComparison.OrdinalIgnoreCase) || property.Value as string == "用户可见内容");
    }

    [Fact]
    public async Task TwoServiceProvidersHaveIndependentFeedsAndLifetimes()
    {
        using var first = BuildServices();
        using var second = BuildServices();
        first.GetRequiredService<ILogger<ProviderTests>>().LogInformation("first");
        second.GetRequiredService<ILogger<ProviderTests>>().LogInformation("second");
        var firstFeed = first.GetRequiredService<LogEventFeed>();
        var secondFeed = second.GetRequiredService<LogEventFeed>();
        Assert.NotSame(firstFeed, secondFeed);
        Assert.Equal("first", (await WaitForEventsAsync(firstFeed, 1))[0].Message);
        Assert.Equal("second", (await WaitForEventsAsync(secondFeed, 1))[0].Message);
    }

    [Fact]
    public async Task StaticLoggerBridgeRoutesToProviderAndDetachesOnDispose()
    {
        var services = BuildServices(options => options.BridgeStaticLogger = true);
        var feed = services.GetRequiredService<LogEventFeed>();
        Logger.Information("static bridge message");
        Assert.Equal("static bridge message", (await WaitForEventsAsync(feed, 1))[0].Message);
        services.Dispose();
        Assert.Throws<InvalidOperationException>(() => Logger.Information("after dispose"));
    }

    [Fact]
    public void AppSettingsShapeIsBoundWithoutReflectionBinder()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:CodeWF:BridgeStaticLogger"] = "false",
                ["Logging:CodeWF:LineTemplate"] = "{Level:u3}|{Message}{NewLine}",
                ["Logging:CodeWF:File:Enabled"] = "false",
                ["Logging:CodeWF:Console:Enabled"] = "true",
                ["Logging:CodeWF:Console:MinimumLevel"] = "Warning",
                ["Logging:CodeWF:EventFeed:RecentCapacity"] = "321",
                ["Logging:CodeWF:Queue:FullMode"] = "DropNewest",
                ["Logging:CodeWF:Queue:EnqueueTimeout"] = "00:00:00.250"
            })
            .Build();
        using var services = new ServiceCollection()
            .AddSingleton(configuration)
            .AddLogging(builder => builder.AddCodeWF())
            .BuildServiceProvider();

        var options = services.GetRequiredService<CodeWFLoggerRuntime>().Options;
        Assert.False(options.BridgeStaticLogger);
        Assert.False(options.File.Enabled);
        Assert.True(options.Console.Enabled);
        Assert.Equal(LogLevel.Warning, options.Console.MinimumLevel);
        Assert.Equal(321, options.EventFeed.RecentCapacity);
        Assert.Equal(LogQueueFullMode.DropNewest, options.Queue.FullMode);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.Queue.EnqueueTimeout);
    }

    [Fact]
    public async Task CaptureConfigurationIsSnapshottedWhenRuntimeStarts()
    {
        using var services = BuildServices(options =>
        {
            options.Capture.Scopes = false;
            options.Capture.Activity = false;
        });
        var runtime = services.GetRequiredService<CodeWFLoggerRuntime>();
        runtime.Options.Capture.Scopes = true;
        runtime.Options.Capture.Activity = true;

        var logger = services.GetRequiredService<ILogger<ProviderTests>>();
        using var activity = new Activity("ignored-after-startup").Start();
        using var scope = logger.BeginScope("ignored-after-startup");
        logger.LogInformation("capture settings stay fixed");

        var logEvent = (await WaitForEventsAsync(runtime.Events, 1))[0];
        Assert.Empty(logEvent.Scopes);
        Assert.Null(logEvent.TraceId);
    }

    private static ServiceProvider BuildServices(Action<CodeWFLoggerOptions>? configure = null) =>
        new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddCodeWF(options =>
                {
                    options.BridgeStaticLogger = false;
                    options.File.Enabled = false;
                    options.Console.Enabled = false;
                    options.EventFeed.RecentCapacity = 100;
                    configure?.Invoke(options);
                });
            })
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private static async Task<IReadOnlyList<CodeWFLogEvent>> WaitForEventsAsync(LogEventFeed feed, int count)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            var events = feed.GetRecentEvents();
            if (events.Count >= count) return events;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Expected {count} events, received {feed.GetRecentEvents().Count}.");
    }

    private sealed class StateObserverProvider : ILoggerProvider
    {
        public TaskCompletionSource<Observation> Observation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ILogger CreateLogger(string categoryName) => new StateObserverLogger(Observation);
        public void Dispose() { }
    }

    private sealed class StateObserverLogger(TaskCompletionSource<Observation> observation) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values ? values.ToArray() : [];
            observation.TrySetResult(new Observation(formatter(state, exception), properties));
        }
    }

    private sealed record Observation(string Message, IReadOnlyList<KeyValuePair<string, object?>> Properties);
}
