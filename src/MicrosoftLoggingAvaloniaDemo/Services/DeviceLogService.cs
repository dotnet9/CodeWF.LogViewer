using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MicrosoftLoggingAvaloniaDemo.Services;

public sealed class DeviceLogService
{
    private static readonly Action<ILogger, int, string, Exception?> DeviceSampled =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(2301, "DeviceSampled"),
            "LoggerMessage sampled device {DeviceNumber} with value {Value}");

    private readonly ILogger<DeviceLogService> _logger;
    private int _operationNumber;

    public DeviceLogService(ILogger<DeviceLogService> logger) => _logger = logger;

    public void WriteLevel(LogLevel level)
    {
        var operation = NextOperation();
        var deviceId = $"PLC-{Random.Shared.Next(1, 100):00}";
        var elapsed = Random.Shared.Next(20, 1_500);
        _logger.Log(
            level,
            new EventId(2000 + operation % 100, "ManualLevel"),
            "Operation {Operation} on {DeviceId} completed in {ElapsedMilliseconds} ms at {LoggedAt}",
            operation,
            deviceId,
            elapsed,
            DateTimeOffset.Now);
    }

    public void WriteAllLevels()
    {
        foreach (var level in Enum.GetValues<LogLevel>().Where(level => level != LogLevel.None))
            WriteLevel(level);
    }

    public void WriteMessageComparison()
    {
        var operation = NextOperation();
        var taskName = $"task-{operation:000}";
        _logger.LogWarning(
            new EventId(2101, "DiagnosticWarning"),
            "Task {TaskName} response exceeded {ElapsedMilliseconds} ms",
            taskName,
            Random.Shared.Next(700, 1_500));
        _logger.LogUserWarning(
            $"任务“{taskName}”响应较慢，请稍后重试。",
            "Task {TaskName} response exceeded {ElapsedMilliseconds} ms",
            taskName,
            Random.Shared.Next(700, 1_500));
    }

    public void WriteFriendlyException()
    {
        var operation = NextOperation();
        var taskPath = $@"E:\TaskFolder\task-{operation:000}\task.xml";
        try
        {
            throw new InvalidOperationException(
                $"Schema validation failed at element #{Random.Shared.Next(2, 20)}: unsupported PointDirection value.");
        }
        catch (Exception ex)
        {
            _logger.LogUserError(
                ex,
                $"无法打开任务“task-{operation:000}”：任务文件内容不正确或与当前版本不兼容。",
                "Failed to parse task file {TaskPath} during operation {Operation}",
                taskPath,
                operation);
        }
    }

    public void WriteContext()
    {
        var operation = NextOperation();
        var deviceId = $"PLC-{Random.Shared.Next(1, 100):00}";
        using var activity = new Activity("Demo.DeviceHeartbeat")
            .SetIdFormat(ActivityIdFormat.W3C)
            .AddTag("demo.operation", operation)
            .AddBaggage("demo.station", $"Station-{Random.Shared.Next(1, 8)}")
            .Start();
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["Operation"] = operation,
            ["DeviceId"] = deviceId,
            ["Operator"] = Environment.UserName
        });

        _logger.LogInformation(
            new EventId(2201, "DeviceHeartbeat"),
            "Heartbeat from {DeviceId} received with signal {SignalStrength}",
            deviceId,
            Random.Shared.Next(60, 100));
    }

    public void WriteLoggerMessage()
    {
        var operation = NextOperation();
        DeviceSampled(_logger, operation, $"{Random.Shared.NextDouble() * 100:F3}", null);
    }

    public Task WriteBurstAsync()
    {
        var batchId = Guid.NewGuid().ToString("N")[..8];
        return Task.Run(() => Parallel.ForEach(
            Enumerable.Range(1, 300),
            index =>
            {
                _logger.LogInformation(
                    "Batch {BatchId} item {Index}/{Total} produced value {Value}",
                    batchId,
                    index,
                    300,
                    Random.Shared.NextDouble());
                if (index % 60 == 0)
                    _logger.LogError(
                        new EventId(2401, "BurstCheckpoint"),
                        "Batch {BatchId} reached error checkpoint {Index}/{Total}",
                        batchId,
                        index,
                        300);
            }));
    }

    private int NextOperation() => Interlocked.Increment(ref _operationNumber);
}
