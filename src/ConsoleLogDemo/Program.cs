using CodeWF.Log.Core;

// 控制台日志输出测试程序
Logger.Info("这样写可不输出控制台：Logger.EnableConsoleOutput = false;");
Logger.EnableConsoleOutput = false;
Logger.Info("=== 控制台日志输出测试 ===");
Logger.Info("");

// 设置日志级别为Debug，确保所有日志都能输出
Logger.Level = LogType.Debug;

// 1. 测试不同日志级别的彩色输出
Logger.Info("1. 测试不同日志级别的彩色输出：");
Logger.Info("============================");
Logger.Debug("这是一条调试日志");
Logger.Info("这是一条信息日志");
Logger.Warn("这是一条警告日志");
Logger.Error("这是一条错误日志");
Logger.Fatal("这是一条致命错误日志");
Logger.Info("");

// 2. 测试EnableConsoleOutput开关效果
Logger.Info("2. 测试EnableConsoleOutput开关效果：");
Logger.Info("============================");
Logger.Info("禁用控制台输出...");
Logger.EnableConsoleOutput = false;
Logger.Info("这条日志不会显示在控制台（已禁用）");
Logger.Info("重新启用控制台输出...");
Logger.EnableConsoleOutput = true;
Logger.Info("这条日志会显示在控制台（已重新启用）");
Logger.Info("");

// 3. 测试log2Console参数效果
Logger.Info("3. 测试log2Console参数效果：");
Logger.Info("============================");
Logger.Info("这条日志会显示在控制台（默认）");
Logger.Info("这条日志不会显示在控制台", log2Console: false);
Logger.Info("");

// 4. 测试异常信息的输出
Logger.Info("4. 测试异常信息的输出：");
Logger.Info("============================");
try
{
    throw new InvalidOperationException("这是一个测试异常");
}
catch (Exception ex)
{
    Logger.Error("捕获到异常：", ex);
    Logger.Fatal("捕获到致命异常：", ex);
}
Logger.Info("");

// 5. 测试批量日志输出
Logger.Info("5. 测试批量日志输出：");
Logger.Info("============================");
for (int i = 0; i < 5; i++)
{
    Logger.Debug($"批量调试日志 #{i+1}");
    Logger.Info($"批量信息日志 #{i+1}");
    Logger.Warn($"批量警告日志 #{i+1}");
}
Logger.Info("");

// 6. 测试便捷方法
Logger.Info("6. 测试便捷方法：");
Logger.Info("============================");
Logger.DebugToFile("这条调试日志只输出到文件，不会显示在控制台");
Logger.InfoToUI("这条信息日志会显示在控制台（因为ToUI方法默认输出到控制台）");
Logger.Info("");

// 7. 测试自定义UI内容
Logger.Info("7. 测试自定义UI内容：");
Logger.Info("============================");
Logger.Warn("详细的警告信息，包含技术细节", "用户友好的警告提示");
Logger.Info("");

Logger.Info("=== 测试完成 ===");
Logger.Info("按任意键退出...");
Console.ReadKey();