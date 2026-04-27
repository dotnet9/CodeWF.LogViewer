using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeWF.Log.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace AvaloniaLogDemo.Views
{
    public partial class MainWindow : Window
    {
        private readonly Timer _trafficTimer;
        private readonly DateTime _startedAt = DateTime.Now;

        private ComboBox _levelSelector = null!;
        private CheckBox _outputToUiCheckBox = null!;
        private CheckBox _outputToFileCheckBox = null!;
        private CheckBox _outputToConsoleCheckBox = null!;
        private Button _liveTrafficButton = null!;
        private Button _burstTestButton = null!;
        private TextBlock _levelStateText = null!;
        private TextBlock _fileStateText = null!;
        private TextBlock _uiStateText = null!;
        private TextBlock _logPathText = null!;
        private TextBlock _throughputText = null!;
        private TextBlock _lastEventText = null!;
        private TextBlock _healthText = null!;
        private TextBlock _totalLogsText = null!;
        private TextBlock _problemLogsText = null!;
        private TextBlock _infoLogsText = null!;
        private TextBlock _fatalLogsText = null!;
        private TextBlock _debugLogsText = null!;
        private TextBlock _warnLogsText = null!;
        private TextBlock _errorLogsText = null!;
        private TextBlock _liveBatchText = null!;

        private int _totalCount;
        private int _debugCount;
        private int _infoCount;
        private int _warnCount;
        private int _errorCount;
        private int _fatalCount;
        private int _trafficBatch;
        private bool _isLiveTrafficRunning;
        private bool _isBurstRunning;
        private bool _controlsReady;
        private string _lastEvent = "Last: waiting for traffic";

        private static readonly string[] Services =
        [
            "Identity.Api",
            "Order.Api",
            "Billing.Worker",
            "Inventory.Sync",
            "Notification.Job"
        ];

        private static readonly string[] Routes =
        [
            "POST /api/orders",
            "GET /api/orders/{id}",
            "POST /api/payments/capture",
            "PUT /api/inventory/reserve",
            "POST /api/messages/send"
        ];

        private static readonly string[] Tenants =
        [
            "north-cn",
            "east-cn",
            "global",
            "sandbox"
        ];

        public MainWindow()
        {
            InitializeComponent();
            ResolveControls();
            ConfigureLogger();

            _trafficTimer = new Timer(1200) { AutoReset = true };
            _trafficTimer.Elapsed += TrafficTimer_Elapsed;
            Closing += MainWindow_Closing;

            _controlsReady = true;
            UpdateDashboard();
            SeedStartupLogs();
        }

        private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            _trafficTimer.Stop();
            _trafficTimer.Dispose();
            await Logger.FlushAsync();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ResolveControls()
        {
            _levelSelector = FindRequired<ComboBox>("LevelSelector");
            _outputToUiCheckBox = FindRequired<CheckBox>("OutputToUiCheckBox");
            _outputToFileCheckBox = FindRequired<CheckBox>("OutputToFileCheckBox");
            _outputToConsoleCheckBox = FindRequired<CheckBox>("OutputToConsoleCheckBox");
            _liveTrafficButton = FindRequired<Button>("LiveTrafficButton");
            _burstTestButton = FindRequired<Button>("BurstTestButton");
            _levelStateText = FindRequired<TextBlock>("LevelStateText");
            _fileStateText = FindRequired<TextBlock>("FileStateText");
            _uiStateText = FindRequired<TextBlock>("UiStateText");
            _logPathText = FindRequired<TextBlock>("LogPathText");
            _throughputText = FindRequired<TextBlock>("ThroughputText");
            _lastEventText = FindRequired<TextBlock>("LastEventText");
            _healthText = FindRequired<TextBlock>("HealthText");
            _totalLogsText = FindRequired<TextBlock>("TotalLogsText");
            _problemLogsText = FindRequired<TextBlock>("ProblemLogsText");
            _infoLogsText = FindRequired<TextBlock>("InfoLogsText");
            _fatalLogsText = FindRequired<TextBlock>("FatalLogsText");
            _debugLogsText = FindRequired<TextBlock>("DebugLogsText");
            _warnLogsText = FindRequired<TextBlock>("WarnLogsText");
            _errorLogsText = FindRequired<TextBlock>("ErrorLogsText");
            _liveBatchText = FindRequired<TextBlock>("LiveBatchText");
        }

        private T FindRequired<T>(string name) where T : Control
        {
            return this.FindControl<T>(name) ?? throw new InvalidOperationException($"{name} is missing.");
        }

        private void ConfigureLogger()
        {
            Logger.Level = LogType.Debug;
            Logger.BatchProcessSize = 80;
            Logger.LogUIDuration = 80;
            Logger.MaxUIDisplayCount = 1600;
            Logger.MaxLogFileSizeMB = 5;
            Logger.TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
            Logger.EnableConsoleOutput = LogToConsole;
        }

        private bool LogToUi => _outputToUiCheckBox.IsChecked == true;

        private bool LogToFile => _outputToFileCheckBox.IsChecked == true;

        private bool LogToConsole => _outputToConsoleCheckBox.IsChecked == true;

        private void SeedStartupLogs()
        {
            Emit(LogType.Info,
                "console=CodeWF.LogViewer.Avalonia env=demo status=initialized fileRotation=5MB batchSize=80",
                "日志观察台已初始化，文件轮转 5MB，批处理阈值 80 条。");
            Emit(LogType.Debug,
                $"logDir=\"{Path.Combine(Logger.LogDir, "Log")}\" uiChannel={LogToUi} fileChannel={LogToFile}");
            Emit(LogType.Info,
                "service=Order.Api trace=9f4c1a0b7e2d route=\"GET /api/orders/{id}\" status=200 elapsed=38ms tenant=north-cn",
                "Order.Api 请求完成，耗时 38ms。");
        }

        private void LevelSelector_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_controlsReady)
            {
                return;
            }

            Logger.Level = _levelSelector.SelectedIndex switch
            {
                1 => LogType.Info,
                2 => LogType.Warn,
                3 => LogType.Error,
                4 => LogType.Fatal,
                _ => LogType.Debug
            };

            Emit(LogType.Info,
                $"logger.level changed level={Logger.Level}",
                $"采样级别已切换为 {GetLevelName(Logger.Level)}。");
            UpdateDashboard();
        }

        private void Channels_OnChanged(object? sender, RoutedEventArgs e)
        {
            if (!_controlsReady)
            {
                return;
            }

            Logger.EnableConsoleOutput = LogToConsole;
            Emit(LogType.Info,
                $"logger.channels ui={LogToUi} file={LogToFile} console={LogToConsole}",
                $"输出通道已更新：UI={ToOnOff(LogToUi)}，File={ToOnOff(LogToFile)}，Console={ToOnOff(LogToConsole)}。");
            UpdateDashboard();
        }

        private void ApiScenario_OnClick(object? sender, RoutedEventArgs e)
        {
            var traceId = CreateTraceId();
            var latency = Random.Shared.Next(28, 420);
            var route = Pick(Routes);
            var tenant = Pick(Tenants);

            Emit(LogType.Debug,
                $"trace={traceId} service=Gateway route=\"{route}\" tenant={tenant} requestSize={Random.Shared.Next(2, 24)}KB");
            Emit(LogType.Info,
                $"trace={traceId} service=Order.Api route=\"{route}\" status=200 elapsed={latency}ms tenant={tenant}",
                $"Order.Api 链路完成，trace={traceId}，耗时 {latency}ms。");
        }

        private void QueueScenario_OnClick(object? sender, RoutedEventArgs e)
        {
            var queue = Random.Shared.Next(1800, 7200);
            var lag = Random.Shared.Next(8, 38);

            Emit(LogType.Warn,
                $"service=Billing.Worker queue=payment-capture pending={queue} consumerLag={lag}s retryPolicy=exponential",
                $"支付捕获队列积压 {queue} 条，消费者延迟 {lag}s。");
            Emit(LogType.Info,
                $"service=Billing.Worker action=scale-out consumers={Random.Shared.Next(4, 9)} pending={queue}");
        }

        private void DatabaseScenario_OnClick(object? sender, RoutedEventArgs e)
        {
            var elapsed = Random.Shared.Next(1200, 5200);
            var rows = Random.Shared.Next(12000, 98000);

            Emit(LogType.Warn,
                $"db=primary table=Orders index=IX_Orders_TenantId elapsed={elapsed}ms rows={rows} isolation=ReadCommitted",
                $"Orders 慢查询 {elapsed}ms，扫描 {rows} 行。");
        }

        private void ExceptionScenario_OnClick(object? sender, RoutedEventArgs e)
        {
            var traceId = CreateTraceId();
            var exception = new TimeoutException("SQL execution exceeded 3000ms.");

            Emit(LogType.Error,
                $"trace={traceId} service=Order.Api route=\"POST /api/orders\" status=500 dependency=SqlServer",
                $"订单创建失败，trace={traceId}，请检查数据库依赖。",
                exception);
        }

        private void SecurityScenario_OnClick(object? sender, RoutedEventArgs e)
        {
            var account = $"usr-{Random.Shared.Next(10000, 99999)}";
            var ip = $"10.{Random.Shared.Next(1, 32)}.{Random.Shared.Next(0, 255)}.{Random.Shared.Next(1, 255)}";

            Emit(LogType.Warn,
                $"security=audit account={account} ip={ip} action=login-denied reason=mfa-required risk=medium",
                $"账号 {account} 登录被拒绝，风险等级 medium。");
        }

        private void StartLiveTraffic_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_isLiveTrafficRunning)
            {
                _trafficTimer.Stop();
                _isLiveTrafficRunning = false;
                _liveTrafficButton.Content = "启动实时流";
                Emit(LogType.Info, "traffic.replay stopped", "实时流量回放已停止。");
                return;
            }

            _trafficTimer.Start();
            _isLiveTrafficRunning = true;
            _liveTrafficButton.Content = "停止实时流";
            Emit(LogType.Info, "traffic.replay started interval=1200ms", "实时流量回放已启动。");
        }

        private async void BurstTest_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_isBurstRunning)
            {
                return;
            }

            _isBurstRunning = true;
            _burstTestButton.IsEnabled = false;
            _burstTestButton.Content = "压测中...";

            var logToUi = LogToUi;
            var logToFile = LogToFile;
            var logToConsole = LogToConsole;

            await Task.Run(() =>
            {
                for (var i = 1; i <= 500; i++)
                {
                    var level = (LogType)(i % 5);
                    var traceId = CreateTraceId();
                    var elapsed = Random.Shared.Next(3, 880);
                    var service = Pick(Services);
                    var content =
                        $"burst={i:000} trace={traceId} service={service} elapsed={elapsed}ms payload={Random.Shared.Next(1, 64)}KB";

                    WriteLog(level, content, null, null, logToUi, logToFile, logToConsole);
                }
            });

            _burstTestButton.Content = "批量写入 500 条";
            _burstTestButton.IsEnabled = true;
            _isBurstRunning = false;

            Emit(LogType.Info,
                "burst.completed count=500",
                "批量写入完成，已发送 500 条混合级别日志。");
        }

        private void TrafficTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var batch = Interlocked.Increment(ref _trafficBatch);
            var logToUi = false;
            var logToFile = false;
            var logToConsole = false;

            try
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    logToUi = LogToUi;
                    logToFile = LogToFile;
                    logToConsole = LogToConsole;
                });
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (TaskCanceledException)
            {
                return;
            }

            var logsToWrite = Random.Shared.Next(4, 9);
            for (var i = 0; i < logsToWrite; i++)
            {
                var sample = CreateTrafficLog(batch);
                WriteLog(sample.Level, sample.Content, sample.UiContent, sample.Exception, logToUi, logToFile, logToConsole);
            }
        }

        private (LogType Level, string Content, string? UiContent, Exception? Exception) CreateTrafficLog(int batch)
        {
            var service = Pick(Services);
            var route = Pick(Routes);
            var tenant = Pick(Tenants);
            var traceId = CreateTraceId();
            var elapsed = Random.Shared.Next(4, 2200);
            var dice = Random.Shared.Next(100);

            if (dice < 52)
            {
                return (LogType.Info,
                    $"batch={batch} trace={traceId} service={service} route=\"{route}\" status=200 elapsed={elapsed}ms tenant={tenant}",
                    $"{service} 请求完成，耗时 {elapsed}ms。",
                    null);
            }

            if (dice < 70)
            {
                return (LogType.Debug,
                    $"batch={batch} trace={traceId} service={service} cache=hit elapsed={elapsed}ms tenant={tenant}",
                    null,
                    null);
            }

            if (dice < 86)
            {
                return (LogType.Warn,
                    $"batch={batch} trace={traceId} service={service} route=\"{route}\" status=429 elapsed={elapsed}ms tenant={tenant}",
                    $"{service} 出现限流，trace={traceId}。",
                    null);
            }

            if (dice < 97)
            {
                return (LogType.Error,
                    $"batch={batch} trace={traceId} service={service} route=\"{route}\" status=503 elapsed={elapsed}ms dependency=Redis",
                    $"{service} 依赖 Redis 不稳定，trace={traceId}。",
                    new TimeoutException("Redis command timed out after 1200ms."));
            }

            return (LogType.Fatal,
                $"batch={batch} trace={traceId} service={service} route=\"{route}\" status=critical failover=required",
                $"{service} 触发严重故障，trace={traceId}。",
                new InvalidOperationException("Primary region failover did not complete."));
        }

        private void Emit(LogType level, string content, string? uiContent = null, Exception? exception = null)
        {
            WriteLog(level, content, uiContent, exception, LogToUi, LogToFile, LogToConsole);
        }

        private void WriteLog(
            LogType level,
            string content,
            string? uiContent,
            Exception? exception,
            bool logToUi,
            bool logToFile,
            bool logToConsole)
        {
            if (Logger.Level > level)
            {
                _lastEvent = $"Filtered: {GetLevelName(level)} {TrimForStatus(content)}";
                RequestDashboardUpdate();
                return;
            }

            switch (level)
            {
                case LogType.Debug:
                    Logger.Debug(content, uiContent, logToUi, logToFile, logToConsole);
                    break;
                case LogType.Info:
                    Logger.Info(content, uiContent, logToUi, logToFile, logToConsole);
                    break;
                case LogType.Warn:
                    Logger.Warn(content, uiContent, logToUi, logToFile, logToConsole);
                    break;
                case LogType.Error:
                    Logger.Error(content, exception, uiContent, logToUi, logToFile, logToConsole);
                    break;
                case LogType.Fatal:
                    Logger.Fatal(content, exception, uiContent, logToUi, logToFile, logToConsole);
                    break;
                default:
                    Logger.Log(level, content, uiContent, logToUi, logToFile, logToConsole);
                    break;
            }

            CountLog(level);
            _lastEvent = $"Last: {DateTime.Now:HH:mm:ss} {GetLevelName(level)} {TrimForStatus(uiContent ?? content)}";
            RequestDashboardUpdate();
        }

        private void CountLog(LogType level)
        {
            Interlocked.Increment(ref _totalCount);
            switch (level)
            {
                case LogType.Debug:
                    Interlocked.Increment(ref _debugCount);
                    break;
                case LogType.Info:
                    Interlocked.Increment(ref _infoCount);
                    break;
                case LogType.Warn:
                    Interlocked.Increment(ref _warnCount);
                    break;
                case LogType.Error:
                    Interlocked.Increment(ref _errorCount);
                    break;
                case LogType.Fatal:
                    Interlocked.Increment(ref _fatalCount);
                    break;
            }
        }

        private void RequestDashboardUpdate()
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                UpdateDashboard();
                return;
            }

            Dispatcher.UIThread.Post(UpdateDashboard);
        }

        private void UpdateDashboard()
        {
            if (!_controlsReady)
            {
                return;
            }

            var total = Volatile.Read(ref _totalCount);
            var debug = Volatile.Read(ref _debugCount);
            var info = Volatile.Read(ref _infoCount);
            var warn = Volatile.Read(ref _warnCount);
            var error = Volatile.Read(ref _errorCount);
            var fatal = Volatile.Read(ref _fatalCount);
            var problem = warn + error + fatal;
            var elapsedSeconds = Math.Max(1, (DateTime.Now - _startedAt).TotalSeconds);

            _totalLogsText.Text = total.ToString("N0");
            _problemLogsText.Text = problem.ToString("N0");
            _infoLogsText.Text = info.ToString("N0");
            _fatalLogsText.Text = fatal.ToString("N0");
            _debugLogsText.Text = debug.ToString("N0");
            _warnLogsText.Text = warn.ToString("N0");
            _errorLogsText.Text = error.ToString("N0");
            _liveBatchText.Text = Volatile.Read(ref _trafficBatch).ToString("N0");
            _throughputText.Text = $"{total / elapsedSeconds:0.0}/s";
            _lastEventText.Text = _lastEvent;
            _logPathText.Text = Path.Combine(Logger.LogDir, "Log");
            _levelStateText.Text = $"Level: {GetLevelName(Logger.Level)}";
            _fileStateText.Text = $"File: {ToOnOff(LogToFile)}";
            _uiStateText.Text = $"UI: {ToOnOff(LogToUi)}";
            _healthText.Text = fatal > 0
                ? "Critical incidents detected"
                : error > 0
                    ? "Service degradation observed"
                    : warn > 0
                        ? "Warnings under observation"
                        : "Healthy";
        }

        private static string CreateTraceId()
        {
            return Guid.NewGuid().ToString("N")[..12];
        }

        private static T Pick<T>(IReadOnlyList<T> values)
        {
            return values[Random.Shared.Next(values.Count)];
        }

        private static string GetLevelName(LogType level)
        {
            return level switch
            {
                LogType.Debug => "Debug",
                LogType.Info => "Info",
                LogType.Warn => "Warn",
                LogType.Error => "Error",
                LogType.Fatal => "Fatal",
                _ => level.ToString()
            };
        }

        private static string ToOnOff(bool value)
        {
            return value ? "on" : "off";
        }

        private static string TrimForStatus(string value)
        {
            const int maxLength = 92;
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }
    }
}
