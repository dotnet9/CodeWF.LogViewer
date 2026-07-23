using CodeWF.Log.Extensions.Logging;
using Microsoft.Extensions.Logging;

namespace MicrosoftLoggingAvaloniaDemo.Services;

public sealed class DeviceLogService
{
    private readonly ILogger<DeviceLogService> _logger;

    public DeviceLogService(ILogger<DeviceLogService> logger)
    {
        _logger = logger;
    }

    public void WriteAllLevels()
    {
        _logger.LogTrace("设备缓存扫描完成，记录 Trace 诊断日志。");
        _logger.LogDebug("连接池状态正常，ActiveConnections={ActiveConnections}", 3);
        _logger.LogUserInformation("设备服务已启动。", "Device service started at {StartedAt}", DateTimeOffset.Now);
        _logger.LogUserWarning("设备响应较慢，请检查网络连接。", "Device response time reached {ElapsedMilliseconds} ms", 850);
        _logger.LogUserError(null, "数据库连接已中断，请检查数据库服务是否运行。", "Database connection lost: endpoint={Endpoint}", "127.0.0.1:5020");
        _logger.LogUserCritical(null, "模型文件无法加载，程序不能继续运行。", "Required model file cannot be loaded: {ModelPath}", @"D:\Models\device.bin");
    }

    public void WriteDiagnosticOnly()
    {
        try
        {
            throw new InvalidOperationException("This exception should be written to file only.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background synchronization failed for device {DeviceId}", "PLC-01");
        }
    }

    public void WriteFriendlyException()
    {
        try
        {
            throw new InvalidOperationException("Instance validation error: '0' is not a valid value for PointDirection.");
        }
        catch (Exception ex)
        {
            _logger.LogUserError(
                ex,
                "无法打开任务“task3”：任务文件内容不正确或与当前版本不兼容，请重新导出任务文件。",
                "Failed to parse task file {TaskPath}",
                @"E:\TaskFolder\task3\task.xml");
        }
    }

    public Task WriteBurstAsync()
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["BatchId"] = Guid.NewGuid().ToString("N"),
            ["Operator"] = Environment.UserName
        });

        return Task.Run(() => Parallel.ForEach(
            Enumerable.Range(1, 300),
            index =>
            {
                _logger.LogInformation("Batch diagnostic log {Index}", index);
                if (index % 60 == 0)
                    _logger.LogUserInformation(
                        $"批量写入进度：{index}/300。",
                        "Batch write progress {Index}/{Total}",
                        index,
                        300);
            }));
    }
}
