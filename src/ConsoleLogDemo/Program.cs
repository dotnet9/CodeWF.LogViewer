using CodeWF.Log.Core;

var logDirectory = Path.Combine(AppContext.BaseDirectory, "Log");
Logger.Initialize(new LoggerOptions
{
    MinimumLevel = LogType.Debug,
    EnableConsole = true,
    QueueCapacity = 2_000,
    File = new FileLogOptions
    {
        DirectoryPath = logDirectory,
        BatchSize = 20,
        FlushInterval = TimeSpan.FromMilliseconds(200),
        MaxFileSizeBytes = 8 * 1024
    }
});

Console.WriteLine("CodeWF.Log.Core 控制台验证程序");
Console.WriteLine($"日志目录：{logDirectory}");
Console.WriteLine("验证重点：控制台只显示用户消息；技术内容、文件专用日志和异常堆栈只写入文件。");
Console.WriteLine();

Logger.Debug("调试日志：初始化内部状态。", "程序正在准备运行环境。");
Logger.Info("设备服务启动完成。");
Logger.Warn("设备响应时间达到 850ms。", "设备响应较慢，请检查网络连接。");

try
{
    throw new InvalidOperationException(
        "Instance validation error: '0' is not a valid value for PointDirection.");
}
catch (Exception ex)
{
    Logger.Error(
        @"打开任务失败：E:\TaskFolder\task3\task.xml",
        ex,
        "无法打开任务“task3”：任务文件内容不正确或与当前版本不兼容，请重新导出任务文件。");
}

Logger.InfoToFile("文件专用日志：TCP 监听地址=127.0.0.1:2700。");
Logger.ErrorToFile(
    "文件专用内部异常：关闭后台队列失败。",
    new IOException("This exception must never appear in the console."));

Logger.Info("开始并发写入和文件轮转验证。");
await Parallel.ForEachAsync(
    Enumerable.Range(1, 300),
    async (index, cancellationToken) =>
    {
        Logger.InfoToFile($"并发文件日志 #{index:000}，线程={Environment.CurrentManagedThreadId}。");
        if (index % 75 == 0) Logger.Info($"并发写入进度：{index}/300。");
        await Task.Yield();
    });

await Logger.FlushAsync();
Logger.Fatal(
    "演示程序完成全部验证场景。",
    userMessage: "验证完成：请确认控制台没有异常堆栈，并检查日志目录中的技术详情和轮转文件。");
await Logger.ShutdownAsync();

Console.WriteLine();
Console.WriteLine("验证结束，按任意键退出。");
if (!Console.IsInputRedirected) Console.ReadKey(intercept: true);
