using System.Diagnostics;

namespace CodeWF.Log.Core;

internal static class LoggerSelfDiagnostics
{
    private static readonly TimeSpan ConsoleReportInterval = TimeSpan.FromSeconds(30);
    private static long _lastConsoleReportTicks;

    public static void Report(string message, Exception exception)
    {
        Debug.WriteLine($"{message}{Environment.NewLine}{exception}");

        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        var lastTicks = Interlocked.Read(ref _lastConsoleReportTicks);
        if (nowTicks - lastTicks < ConsoleReportInterval.Ticks ||
            Interlocked.CompareExchange(ref _lastConsoleReportTicks, nowTicks, lastTicks) != lastTicks)
            return;

        try
        {
            Console.Error.WriteLine($"日志组件发生故障：{message} 请检查日志目录权限和磁盘空间。");
        }
        catch
        {
            // 日志组件最后的兜底出口不可再向外抛出异常。
        }
    }
}
