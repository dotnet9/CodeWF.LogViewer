using CodeWF.Log.Core;
using CodeWF.Log.Extensions.Logging;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// 本 Demo 故意只通过 appsettings.json 配置 CodeWF；AddCodeWF(options => ...) 同样受支持。
builder.Logging.AddCodeWF();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "CodeWF Microsoft.Extensions.Logging Web API Demo",
    Endpoints = new[]
    {
        "GET /diagnostic/{taskId}",
        "GET /user-error/{taskId}",
        "GET /scope/{deviceId}",
        "GET /recent-logs"
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
        Message = "普通 LogError 已进入 CodeWF 文件与统一事件流。"
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
        detail: "该接口演示 LogUserError：同一个事件同时携带诊断 Message、Exception 和 UserMessage。",
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

app.MapGet("/recent-logs", (LogEventFeed events) => Results.Ok(events.GetRecentEvents().Select(entry => new
{
    entry.Sequence,
    Time = entry.Timestamp,
    Level = entry.Level.ToString(),
    entry.Message,
    entry.UserMessage,
    entry.CategoryName,
    EventId = entry.EventId.Id,
    entry.TraceId
})));

await app.RunAsync();
