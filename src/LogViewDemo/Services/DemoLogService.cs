using System.Diagnostics;
using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.Logging;

namespace LogViewDemo.Services;

public sealed class DemoLogService(ILogger<DemoLogService> logger)
{
    private int _operation;

    public void WriteStartup() =>
        logger.LogInformation("LogViewDemo started at {StartedAt}", DateTimeOffset.Now);

    public void WriteAllLevels()
    {
        foreach (var level in Enum.GetValues<LogLevel>().Where(level => level != LogLevel.None))
        {
            var operation = NextOperation();
            logger.Log(level, new EventId(2000 + operation, "LevelSample"),
                "Operation {Operation} produced {Level} for device {DeviceId}",
                operation, level, $"PLC-{Random.Shared.Next(1, 100):00}");
        }
    }

    public void WriteMessageComparison()
    {
        var operation = NextOperation();
        logger.LogWarning("Task {TaskName} response exceeded {Elapsed} ms", $"task-{operation:000}", 980);
        logger.LogUserWarning(
            $"任务“task-{operation:000}”响应较慢，请稍后重试。",
            "Task {TaskName} response exceeded {Elapsed} ms",
            $"task-{operation:000}", 980);
    }

    public void WriteContextAndException()
    {
        var operation = NextOperation();
        using var activity = new Activity("Demo.DeviceRead")
            .SetIdFormat(ActivityIdFormat.W3C)
            .AddTag("demo.operation", operation)
            .AddBaggage("demo.station", "Station-3")
            .Start();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Operation"] = operation,
            ["Operator"] = Environment.UserName
        });

        logger.LogWarning(new EventId(2201, "DeviceLatency"),
            "Device {DeviceId} response took {Elapsed} ms", "PLC-07", 1260);

        var exception = new InvalidOperationException("Schema validation failed: unsupported PointDirection value.");
        logger.LogUserError(
            exception,
            "任务文件内容不正确或与当前版本不兼容。",
            "Failed to parse task file {TaskPath}",
            $@"E:\TaskFolder\task-{operation:000}\task.xml");
    }

    public void WriteNotificationError(bool requestNotification)
    {
        var operation = NextOperation();
        const string template = "Notification comparison {Operation}, requested={RequestNotification}";
        if (requestNotification)
        {
            logger.LogUserNotification(
                LogLevel.Error,
                "设备服务连接已中断，请检查服务状态。",
                template,
                operation,
                true);
            return;
        }

        logger.LogUserError(
            null,
            "设备服务首次连接失败，后台将继续重试。",
            template,
            operation,
            false);
    }

    public Task WriteBurstAsync(int count)
    {
        var batch = Guid.NewGuid().ToString("N")[..8];
        return Task.Run(() => Parallel.ForEach(Enumerable.Range(1, count), index =>
            logger.LogInformation("Batch {BatchId} item {Index}/{Total}", batch, index, count)));
    }

    private int NextOperation() => Interlocked.Increment(ref _operation);
}
