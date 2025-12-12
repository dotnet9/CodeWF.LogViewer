using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeWF.Log.Core;
using System;
using System.Timers;

namespace AvaloniaLogDemo.Views
{
    public partial class MainWindow : Window
    {
        private readonly Timer _performanceTimer;
        private bool _isPerformanceTesting = false;
        private int _logCounter = 0;
        private const int LogTypesCount = 5;
        private CheckBox? _log2UI;
        private CheckBox? _log2File;

        public MainWindow()
        {
            InitializeComponent();

            _log2UI = this.FindControl<CheckBox>("Log2UI");
            _log2File = this.FindControl<CheckBox>("Log2File");
            Logger.Level = LogType.Debug;
            Logger.MaxLogFileSizeMB = 5;
            Logger.TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
            _performanceTimer = new Timer(100); // 100毫秒间隔
            _performanceTimer.Elapsed += PerformanceTimer_Elapsed;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AddDebugLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Debug(
                $"模拟请求A - {Random.Shared.Next(1, 11)}条记录，响应耗时{Random.Shared.Next(1, 1000)}毫秒", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
        }

        private void AddInfoLog_OnClick(object? sender, RoutedEventArgs e)
        {
            string GetRandomAction() => new[] { "登录", "查看", "新增" }.RandomChoice();
            Logger.Info($"操作B - 用户ID：{Guid.NewGuid()}，执行操作：{GetRandomAction()}", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
        }

        private void AddWarnLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Warn($"数据库 - 查询[用户]时发现重复记录ID: {Random.Shared.Next(10000, 99999)}", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
        }

        private void AddErrorLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Error($"[接口调用] - 访问[订单接口]时发生异常信息：{new Exception("服务器错误").Message}"
                , uiContent: "[友好日志，只显示在UI上]订单接口不正确，请联系管理员", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
        }

        private void AddFatalLog_OnClick(object? sender, RoutedEventArgs e)
        {
            Logger.Fatal(Random.Shared.Next(0, 10) == 0
                ? $"系统级[错误] - 系统发生严重故障：{Random.Shared.Next(1000, 9999)}"
                : $"系统级[错误] - 数据库连接中断，服务停止运行"
                , log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
        }
        
        private void StartPerformanceTest_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (_isPerformanceTesting)
                {
                    // 停止测试
                    _performanceTimer.Stop();
                    _isPerformanceTesting = false;
                    button.Content = "性能测试";
                    Logger.Info("性能测试已停止。", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
                }
                else
                {
                    // 开始测试
                    _logCounter = 0;
                    _performanceTimer.Start();
                    _isPerformanceTesting = true;
                    button.Content = "停止测试";
                    Logger.Info("性能测试已开始，将定时快速调用不同级别的日志接口。", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
                }
            }
        }
        
        private void PerformanceTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // 随机生成本次要写入的日志数量（20-300条之间）
            int logsToWrite = Random.Shared.Next(20, 301);
            int batchCounter = _logCounter;
            
            // 确保在UI线程上更新
            Dispatcher.UIThread.Post(() =>
            {
                for (int i = 0; i < logsToWrite; i++)
                {
                    batchCounter++;
                    int logTypeIndex = batchCounter % LogTypesCount;
                    
                    switch (logTypeIndex)
                    {
                        case 0:
                            Logger.Debug($"性能测试[Debug] - 批次计数: {batchCounter}", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
                            break;
                        case 1:
                            Logger.Info($"性能测试[Info] - 批次计数: {batchCounter}", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
                            break;
                        case 2:
                            Logger.Warn($"性能测试[Warn] - 批次计数: {batchCounter}", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
                            break;
                        case 3:
                            Logger.Error($"性能测试[Error] - 批次计数: {batchCounter}", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
                            break;
                        case 4:
                            Logger.Fatal($"性能测试[Fatal] - 批次计数: {batchCounter}", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
                            break;
                    }
                }
                
                // 更新全局计数器
                _logCounter = batchCounter;
                Logger.Info($"性能测试 - 批次完成，本次写入 {logsToWrite} 条日志，累计写入 {_logCounter} 条日志。", log2UI: _log2UI.IsChecked == true, log2File: _log2File.IsChecked == true);
            });
        }
    }

    public static class RandomExt
    {
        public static T RandomChoice<T>(this T[] array) => array[Random.Shared.Next(array.Length)];
    }
}