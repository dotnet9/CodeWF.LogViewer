using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddCodeWF(options =>
{
    options.File.DirectoryPath = Path.Combine(builder.Environment.ContentRootPath, "Log");
    options.File.BatchSize = 50;
    options.File.FlushInterval = TimeSpan.FromMilliseconds(300);
    options.File.OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}";
    options.Console.Enabled = false;
});

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "CodeWF Microsoft.Extensions.Logging Web API Demo",
    Endpoints = new[]
    {
        "GET /diagnostic/{taskId}",
        "GET /user-error/{taskId}",
        "GET /scope/{deviceId}",
        "GET /recent-user-logs"
    }
}));

app.MapGet("/diagnostic/{taskId}", (string taskId, ILogger<Program> logger) =>
{
    try
    {
        throw new InvalidOperationException("This exception is diagnostic only.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to synchronize task {TaskId}", taskId);
    }

    return Results.Ok(new
    {
        TaskId = taskId,
        Message = "普通 LogError 已写入诊断日志，默认不会进入 UserLogFeed。"
    });
});

app.MapGet("/user-error/{taskId}", (string taskId, ILogger<Program> logger) =>
{
    try
    {
        throw new InvalidOperationException("Task file schema version is unsupported.");
    }
    catch (Exception ex)
    {
        logger.LogUserError(
            ex,
            $"任务 {taskId} 加载失败，请检查任务文件格式后重试。",
            "Failed to parse task {TaskId}",
            taskId);
    }

    return Results.Problem(
        title: "Task load failed",
        detail: "该接口演示 LogUserError：文件记录技术细节，UserLogFeed 只记录用户消息。",
        statusCode: StatusCodes.Status422UnprocessableEntity);
});

app.MapGet("/scope/{deviceId}", (string deviceId, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Demo.DeviceHeartbeat");
    using var activity = new Activity("DemoDeviceHeartbeat").Start();
    using var scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["DeviceId"] = deviceId,
        ["TraceSource"] = "WebApiDemo"
    });

    logger.LogInformation("Received heartbeat from device {DeviceId}", deviceId);
    logger.LogUserInformation(
        $"设备 {deviceId} 心跳正常。",
        "Device {DeviceId} heartbeat received at {Timestamp}",
        deviceId,
        DateTimeOffset.Now);

    return Results.Ok(new
    {
        DeviceId = deviceId,
        ActivityTraceId = Activity.Current?.TraceId.ToString(),
        Message = "Scope、Activity 和结构化属性已写入 CodeWF 诊断日志。"
    });
});

app.MapGet("/recent-user-logs", () => Results.Ok(Logger.UserLogs.GetRecentEntries().Select(entry => new
{
    entry.Sequence,
    Time = entry.Timestamp,
    Level = entry.Level.ToString(),
    entry.Message,
    entry.CategoryName,
    EventId = entry.EventId.Id,
    entry.TraceId
})));

await app.RunAsync();
