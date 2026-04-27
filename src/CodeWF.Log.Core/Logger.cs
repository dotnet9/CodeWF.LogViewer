﻿﻿﻿using CodeWF.Log.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CodeWF.Log.Core
{
    public static class Logger
    {
        /// <summary>
        /// 获取或设置用于日志记录操作的当前日志级别。
        /// </summary>
        public static LogType Level = LogType.Info;

        /// <summary>
        /// 指定存储日志文件的目录。
        /// </summary>
        /// <remarks>默认值是应用程序的基目录。根据需要更新此字段以更改日志文件位置。</remarks>
        public static string LogDir = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// 指定单次批处理操作中要处理的默认日志条目数。
        /// </summary>
        public static uint BatchProcessSize = 200; // 增加批处理大小，减少文件写入频率

        /// <summary>
        /// 获取或设置日志文件在轮换前的最大大小（以MB为单位）。
        /// </summary>
        /// <remarks>当日志文件超过此大小时，将进行轮换以保持文件大小可管理。
        /// 默认值为500 MB。此值必须大于0。</remarks>
        public static uint MaxLogFileSizeMB = 500;

        /// <summary>
        /// 批量记录文本日志的时间间隔（以毫秒为单位）。
        /// </summary>
        public static uint LogFileDuration = 500;

        /// <summary>
        /// 界面最多显示日志条件
        /// </summary>
        public static uint MaxUIDisplayCount = 1000;

        /// <summary>
        /// 批量记录文本日志的时间间隔（以毫秒为单位）。
        /// </summary>
        public static uint LogUIDuration = 100;

        /// <summary>
        /// 时间戳格式字符串，用于日志记录中的时间显示。默认格式为"yyyy-MM-dd HH:mm:ss"。
        /// </summary>
        public static string TimeFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// 是否允许将日志输出到控制台，默认允许
        /// </summary>
        public static bool EnableConsoleOutput { get; set; } = true;

        /// <summary>
        /// 表示内部使用的线程安全日志条目队列。
        /// </summary>
        private static readonly Channel<LogInfo> LogChannel;

        private static readonly Channel<LogInfo> UiChannel;

        private static readonly object _recordingLock = new();
        private static bool _isRecording = false;
        private static bool _isFlushing = false;
        private static CancellationTokenSource? _cts;
        private static Task? _recordingTask;

        static Logger()
        {
            LogChannel = Channel.CreateBounded<LogInfo>(
                new BoundedChannelOptions(10000)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });

            UiChannel = Channel.CreateBounded<LogInfo>(
                new BoundedChannelOptions(5000)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                });
        }

        /// <summary>
        /// 启动后台任务，持续批量处理日志条目并将其写入文件。
        /// </summary>
        /// <remarks>此方法应在应用程序启动时调用一次，以启用异步批量日志文件写入。
        /// 无论是否使用 UI 控件，都需要调用此方法启动文件日志消费。
        /// 该方法不会阻塞调用线程，而是立即返回。
        /// 多次调用无效（内部有防重入检查）。</remarks>
        public static void RecordToFile()
        {
            lock (_recordingLock)
            {
                if (_isRecording)
                {
                    return;
                }

                _isRecording = true;
                _cts = new CancellationTokenSource();
                var cts = _cts;

                _recordingTask = Task.Run(async () =>
                {
                    var batch = new List<LogInfo>();
                    var batchLock = new object();
                    var debounceTask = Task.CompletedTask;

                    try
                    {
                        await foreach (var log in LogChannel.Reader.ReadAllAsync(cts.Token))
                        {
                            List<LogInfo>? batchToFlush = null;

                            lock (batchLock)
                            {
                                batch.Add(log);

                                if (batch.Count >= BatchProcessSize)
                                {
                                    batchToFlush = [.. batch];
                                    batch.Clear();
                                }
                            }

                            if (batchToFlush != null)
                            {
                                FlushBatchToFile(batchToFlush);
                            }
                            else if (debounceTask.IsCompleted)
                            {
                                debounceTask = DebounceFlushAsync(batch, batchLock, cts.Token, (int)LogFileDuration);
                            }
                        }

                        FlushPendingBatch(batch, batchLock);
                    }
                    catch (OperationCanceledException)
                    {
                        FlushPendingBatch(batch, batchLock);
                    }
                    finally
                    {
                        lock (_recordingLock)
                        {
                            if (ReferenceEquals(_cts, cts))
                            {
                                _isRecording = false;
                                _recordingTask = null;
                                _cts = null;
                                cts.Dispose();
                            }
                        }
                    }
                });
            }
        }

        private static async Task DebounceFlushAsync(List<LogInfo> batch, object batchLock, CancellationToken cancellationToken, int delayMs)
        {
            try
            {
                await Task.Delay(delayMs, cancellationToken);
                FlushPendingBatch(batch, batchLock);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private static void FlushPendingBatch(List<LogInfo> batch, object batchLock)
        {
            List<LogInfo>? batchToFlush = null;

            lock (batchLock)
            {
                if (batch.Count > 0)
                {
                    batchToFlush = new List<LogInfo>(batch);
                    batch.Clear();
                }
            }

            if (batchToFlush != null)
            {
                FlushBatchToFile(batchToFlush);
            }
        }

        private static void FlushBatchToFile(IReadOnlyList<LogInfo> batch)
        {
            if (batch.Count == 0) return;

            var builder = new StringBuilder();
            foreach (var log in batch)
            {
                builder.AppendLine($"{log.RecordTime.ToString(TimeFormat)}: {log.Level.Description()} {log.Description}");
            }

            AddLogToFileSync(builder.ToString());
        }

        private static void AddLogToFileSync(string msg)
        {
            try
            {
                var logFolder = Path.Combine(LogDir, "Log");
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                var logFilePath = GetAvailableLogFilePath(logFolder, DateTime.Now);
                File.AppendAllText(logFilePath, msg);
            }
            catch
            {
            }
        }

        public static async IAsyncEnumerable<LogInfo> ReadAllUiLogsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var log in UiChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return log;
            }
        }

        private static readonly object _flushLock = new();

        /// <summary>
        /// 强制将所有待写入文件的日志刷新到磁盘
        /// </summary>
        /// <remarks>此方法仅在程序退出时调用，确保所有日志都被写入文件。
        /// 调用此方法前应先停止产生新日志。它不会阻止日志方法的调用。</remarks>
        public static async Task FlushAsync()
        {
            lock (_flushLock)
            {
                if (_isFlushing) return;
                _isFlushing = true;
            }

            try
            {
                Task? recordingTask;
                CancellationTokenSource? cts;

                lock (_recordingLock)
                {
                    recordingTask = _recordingTask;
                    cts = _cts;
                }

                try
                {
                    cts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                if (recordingTask != null)
                {
                    try
                    {
                        await recordingTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                var logsToFlush = new List<LogInfo>();

                while (LogChannel.Reader.TryRead(out var log))
                {
                    logsToFlush.Add(log);
                }

                if (logsToFlush.Count > 0)
                {
                    await AddLogBatchToFileAsync(logsToFlush);
                }

                while (UiChannel.Reader.TryRead(out _))
                {
                }
            }
            catch
            {
            }
            finally
            {
                lock (_flushLock)
                {
                    _isFlushing = false;
                }
            }
        }

        /// <summary>
        /// 记录日志信息（使用整数类型参数）
        /// </summary>
        /// <param name="type">日志类型整数（将转换为LogType枚举）</param>
        /// <param name="content">日志内容（将写入日志文件）</param>
        /// <param name="uiContent">用于UI显示的友好日志内容（可选，默认为null，此时UI将显示content）</param>
        /// <param name="log2UI">是否输出到UI界面，默认为true</param>
        /// <param name="log2File">是否输出到日志文件，默认为true</param>
        /// <param name="log2Console">是否输出到控制台，默认为true</param>
        public static void Log(int type, string content, string? uiContent = default, bool log2UI = true,
            bool log2File = true, bool log2Console = true)
        {
            var logType = (LogType)type;
            if (Level > logType) return;

            if (EnableConsoleOutput && log2Console)
            {
                WriteToConsole(logType, content);
            }

            var logInfo = new LogInfo(logType, content, uiContent, log2UI, log2File);

            if (log2File)
            {
                LogChannel.Writer.TryWrite(logInfo);
            }

            if (log2UI)
            {
                UiChannel.Writer.TryWrite(logInfo);
            }
        }

        /// <summary>
        /// 只往文件输出日志，不输出到UI
        /// </summary>
        /// <param name="type">日志类型整数</param>
        /// <param name="content">日志内容</param>
        public static void LogToFile(int type, string content)
        {
            Log(type, content, null, log2UI: false, log2File: true);
        }

        /// <summary>
        /// 只往UI输出日志，不输出到文件
        /// </summary>
        /// <param name="type">日志类型整数</param>
        /// <param name="content">日志内容（同时作为UI显示内容）</param>
        public static void LogToUI(int type, string content)
        {
            Log(type, content, content, log2UI: true, log2File: false);
        }

        /// <summary>
        /// 记录日志信息
        /// </summary>
        /// <param name="type">日志类型</param>
        /// <param name="content">日志内容（将写入日志文件）</param>
        /// <param name="uiContent">用于UI显示的友好日志内容（可选，默认为null，此时UI将显示content）</param>
        /// <param name="log2UI">是否输出到UI界面，默认为true</param>
        /// <param name="log2File">是否输出到日志文件，默认为true</param>
        /// <param name="log2Console">是否输出到控制台，默认为true</param>
        public static void Log(LogType type, string content, string? uiContent = default, bool log2UI = true,
            bool log2File = true, bool log2Console = true)
        {
            if (Level > type) return;

            if (EnableConsoleOutput && log2Console)
            {
                WriteToConsole(type, content);
            }

            var logInfo = new LogInfo(type, content, uiContent, log2UI, log2File);

            if (log2File)
            {
                LogChannel.Writer.TryWrite(logInfo);
            }

            if (log2UI)
            {
                UiChannel.Writer.TryWrite(logInfo);
            }
        }

        /// <summary>
        /// 只往文件输出日志，不输出到UI
        /// </summary>
        /// <param name="type">日志类型</param>
        /// <param name="content">日志内容</param>
        public static void LogToFile(LogType type, string content)
        {
            Log(type, content, null, log2UI: false, log2File: true, log2Console: false);
        }

        /// <summary>
        /// 根据日志类型将日志输出到控制台，使用不同的前景色
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <param name="content">日志内容</param>
        private static void WriteToConsole(LogType logType, string content)
        {
            try
            {
                string timestamp = DateTime.Now.ToString(TimeFormat);
                string logContent = $"{timestamp}: {logType.Description()} {content}";
                
                // 根据日志类型设置不同的前景色，参考LogView.axaml.cs中的颜色定义
                switch (logType)
                {
                    case LogType.Debug:
                        Console.ForegroundColor = ConsoleColor.Cyan; // 对应LogView.axaml.cs中的#1890FF（蓝色系）
                        break;
                    case LogType.Info:
                        Console.ForegroundColor = ConsoleColor.Green; // 对应LogView.axaml.cs中的#52C41A（绿色系）
                        break;
                    case LogType.Warn:
                        Console.ForegroundColor = ConsoleColor.Yellow; // 对应LogView.axaml.cs中的#FAAD14（黄色系）
                        break;
                    case LogType.Error:
                        Console.ForegroundColor = ConsoleColor.Red; // 对应LogView.axaml.cs中的#FF4D4F（红色系）
                        break;
                    case LogType.Fatal:
                        Console.ForegroundColor = ConsoleColor.Red; // 对应LogView.axaml.cs中的#FF4D4F（红色系）
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }
                
                Console.WriteLine(logContent);
                
                // 恢复默认颜色
                Console.ResetColor();
            }
            catch
            {
                // 忽略控制台输出异常
            }
        }

        /// <summary>
        /// 只往UI输出日志，不输出到文件
        /// </summary>
        /// <param name="type">日志类型</param>
        /// <param name="content">日志内容（同时作为UI显示内容）</param>
        public static void LogToUI(LogType type, string content)
        {
            Log(type, content, content, log2UI: true, log2File: false);
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        /// <param name="content">日志内容（将写入日志文件）</param>
        /// <param name="uiContent">用于UI显示的友好日志内容（可选，默认为null，此时UI将显示content）</param>
        /// <param name="log2UI">是否输出到UI界面，默认为true</param>
        /// <param name="log2File">是否输出到日志文件，默认为true</param>
        /// <param name="log2Console">是否输出到控制台，默认为true</param>
        public static void Debug(string content, string? uiContent = default, bool log2UI = true, bool log2File = true, bool log2Console = true)
        {
            if (Level <= LogType.Debug)
            {
                if (EnableConsoleOutput && log2Console)
                {
                    WriteToConsole(LogType.Debug, content);
                }

                var logInfo = new LogInfo(LogType.Debug, content, uiContent, log2UI, log2File);

                if (log2File)
                {
                    LogChannel.Writer.TryWrite(logInfo);
                }

                if (log2UI)
                {
                    UiChannel.Writer.TryWrite(logInfo);
                }
            }
        }

        /// <summary>
        /// 只往文件输出调试日志，不输出到UI
        /// </summary>
        /// <param name="content">日志内容</param>
        public static void DebugToFile(string content)
        {
            Debug(content, null, log2UI: false, log2File: true, log2Console: false);
        }

        /// <summary>
        /// 只往UI输出调试日志，不输出到文件
        /// </summary>
        /// <param name="content">日志内容（同时作为UI显示内容）</param>
        public static void DebugToUI(string content)
        {
            Debug(content, content, log2UI: true, log2File: false, log2Console: true);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="content">日志内容（将写入日志文件）</param>
        /// <param name="uiContent">用于UI显示的友好日志内容（可选，默认为null，此时UI将显示content）</param>
        /// <param name="log2UI">是否输出到UI界面，默认为true</param>
        /// <param name="log2File">是否输出到日志文件，默认为true</param>
        /// <param name="log2Console">是否输出到控制台，默认为true</param>
        public static void Info(string content, string? uiContent = default, bool log2UI = true, bool log2File = true, bool log2Console = true)
        {
            if (Level <= LogType.Info)
            {
                if (EnableConsoleOutput && log2Console)
                {
                    WriteToConsole(LogType.Info, content);
                }

                var logInfo = new LogInfo(LogType.Info, content, uiContent, log2UI, log2File);

                if (log2File)
                {
                    LogChannel.Writer.TryWrite(logInfo);
                }

                if (log2UI)
                {
                    UiChannel.Writer.TryWrite(logInfo);
                }
            }
        }

        /// <summary>
        /// 只往文件输出信息日志，不输出到UI
        /// </summary>
        /// <param name="content">日志内容</param>
        public static void InfoToFile(string content)
        {
            Info(content, null, log2UI: false, log2File: true, log2Console: false);
        }

        /// <summary>
        /// 只往UI输出信息日志，不输出到文件
        /// </summary>
        /// <param name="content">日志内容（同时作为UI显示内容）</param>
        public static void InfoToUI(string content)
        {
            Info(content, content, log2UI: true, log2File: false, log2Console: true);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        /// <param name="content">日志内容（将写入日志文件）</param>
        /// <param name="uiContent">用于UI显示的友好日志内容（可选，默认为null，此时UI将显示content）</param>
        /// <param name="log2UI">是否输出到UI界面，默认为true</param>
        /// <param name="log2File">是否输出到日志文件，默认为true</param>
        /// <param name="log2Console">是否输出到控制台，默认为true</param>
        public static void Warn(string content, string? uiContent = default, bool log2UI = true, bool log2File = true, bool log2Console = true)
        {
            if (Level <= LogType.Warn)
            {
                if (EnableConsoleOutput && log2Console)
                {
                    WriteToConsole(LogType.Warn, content);
                }

                var logInfo = new LogInfo(LogType.Warn, content, uiContent, log2UI, log2File);

                if (log2File)
                {
                    LogChannel.Writer.TryWrite(logInfo);
                }

                if (log2UI)
                {
                    UiChannel.Writer.TryWrite(logInfo);
                }
            }
        }

        /// <summary>
        /// 只往文件输出警告日志，不输出到UI
        /// </summary>
        /// <param name="content">日志内容</param>
        public static void WarnToFile(string content)
        {
            Warn(content, null, log2UI: false, log2File: true, log2Console: false);
        }

        /// <summary>
        /// 只往UI输出警告日志，不输出到文件
        /// </summary>
        /// <param name="content">日志内容（同时作为UI显示内容）</param>
        public static void WarnToUI(string content)
        {
            Warn(content, content, log2UI: true, log2File: false, log2Console: true);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="content">错误日志内容（将写入日志文件）</param>
        /// <param name="ex">异常信息（可选，若提供，将在日志中包含异常堆栈信息）</param>
        /// <param name="uiContent">用于UI显示的友好日志内容（可选，默认为null，此时UI将显示content）</param>
        /// <param name="log2UI">是否输出到UI界面，默认为true</param>
        /// <param name="log2File">是否输出到日志文件，默认为true</param>
        /// <param name="log2Console">是否输出到控制台，默认为true</param>
        public static void Error(string content, Exception? ex = null, string? uiContent = default, bool log2UI = true,
            bool log2File = true, bool log2Console = true)
        {
            if (Level > LogType.Error) return;

            var msg = ex == null ? content : $"{content}\r\n{ex.ToString()}";

            if (EnableConsoleOutput && log2Console)
            {
                WriteToConsole(LogType.Error, content);
                if (ex != null)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            var logInfo = new LogInfo(LogType.Error, msg, uiContent, log2UI, log2File);

            if (log2File)
            {
                LogChannel.Writer.TryWrite(logInfo);
            }

            if (log2UI)
            {
                UiChannel.Writer.TryWrite(logInfo);
            }
        }

        /// <summary>
        /// 只往文件输出错误日志，不输出到UI
        /// </summary>
        /// <param name="content">日志内容</param>
        /// <param name="ex">异常信息</param>
        public static void ErrorToFile(string content, Exception? ex = null)
        {
            Error(content, ex, null, log2UI: false, log2File: true, log2Console: false);
        }

        /// <summary>
        /// 只往UI输出错误日志，不输出到文件
        /// </summary>
        /// <param name="content">日志内容（同时作为UI显示内容）</param>
        /// <param name="ex">异常信息</param>
        public static void ErrorToUI(string content, Exception? ex = null)
        {
            Error(content, ex, content, log2UI: true, log2File: false, log2Console: true);
        }

        /// <summary>
        /// 记录致命错误日志
        /// </summary>
        /// <param name="content">致命错误日志内容（将写入日志文件）</param>
        /// <param name="ex">异常信息（可选，若提供，将在日志中包含异常堆栈信息）</param>
        /// <param name="uiContent">用于UI显示的友好日志内容（可选，默认为null，此时UI将显示content）</param>
        /// <param name="log2UI">是否输出到UI界面，默认为true</param>
        /// <param name="log2File">是否输出到日志文件，默认为true</param>
        /// <param name="log2Console">是否输出到控制台，默认为true</param>
        public static void Fatal(string content, Exception? ex = null, string? uiContent = default, bool log2UI = true,
            bool log2File = true, bool log2Console = true)
        {
            if (Level > LogType.Fatal) return;

            var msg = ex == null ? content : $"{content}\r\n{ex.ToString()}";

            if (EnableConsoleOutput && log2Console)
            {
                WriteToConsole(LogType.Fatal, content);
                if (ex != null)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            var logInfo = new LogInfo(LogType.Fatal, msg, uiContent, log2UI, log2File);

            if (log2File)
            {
                LogChannel.Writer.TryWrite(logInfo);
            }

            if (log2UI)
            {
                UiChannel.Writer.TryWrite(logInfo);
            }
        }

        /// <summary>
        /// 只往文件输出致命错误日志，不输出到UI
        /// </summary>
        /// <param name="content">日志内容</param>
        /// <param name="ex">异常信息</param>
        public static void FatalToFile(string content, Exception? ex = null)
        {
            Fatal(content, ex, null, log2UI: false, log2File: true, log2Console: false);
        }

        /// <summary>
        /// 只往UI输出致命错误日志，不输出到文件
        /// </summary>
        /// <param name="content">日志内容（同时作为UI显示内容）</param>
        /// <param name="ex">异常信息</param>
        public static void FatalToUI(string content, Exception? ex = null)
        {
            Fatal(content, ex, content, log2UI: true, log2File: false, log2Console: true);
        }

        /// <summary>
        /// 将单个日志信息异步写入文件
        /// </summary>
        /// <param name="logInfo">日志信息对象</param>
        public static async Task AddLogToFileAsync(LogInfo logInfo)
        {
            await AddLogToFileAsync(
                $"{logInfo.RecordTime.ToString(TimeFormat)}: {logInfo.Level.Description()} {logInfo.Description}{Environment.NewLine}");
        }

        /// <summary>
        /// 批量将日志写入文件，提高大量日志写入时的性能
        /// </summary>
        /// <param name="logsBatch">日志批次</param>
        public static async Task AddLogBatchToFileAsync(List<LogInfo>? logsBatch)
        {
            if (logsBatch == null || logsBatch.Count == 0)
                return;

            try
            {
                // 使用StringBuilder批量构建日志内容，减少字符串拼接开销
                var logContentBuilder = new StringBuilder();

                foreach (var logInfo in logsBatch)
                {
                    logContentBuilder.AppendLine(
                        $"{logInfo.RecordTime.ToString(TimeFormat)}: {logInfo.Level.Description()} {logInfo.Description}");
                }

                // 只调用一次文件写入，大幅减少I/O操作
                await AddLogToFileAsync(logContentBuilder.ToString());
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// 将日志内容异步写入文件
        /// </summary>
        /// <param name="msg">要写入的日志内容</param>
        /// <remarks>此方法会自动创建日志目录（如果不存在），
        /// 并根据当前日期和文件大小动态选择合适的日志文件。
        /// 如果写入过程中发生异常，异常将被捕获并忽略，以避免影响主程序运行。</remarks>
        public static async Task AddLogToFileAsync(string msg)
        {
            await AddLogToFileOptimizedAsync(msg);
        }

        /// <summary>
        /// 将日志内容异步写入文件（优化版本，使用类级缓存）
        /// </summary>
        /// <param name="msg">要写入的日志内容</param>
        private static async Task AddLogToFileOptimizedAsync(string msg)
        {
            try
            {
                var logFolder = Path.Combine(LogDir, "Log");

                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                var logFilePath = GetAvailableLogFilePath(logFolder, DateTime.Now);

                await using var stream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write,
                    FileShare.Read, 4096, useAsync: true);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(msg);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// 动态获取可用的日志文件路径，从基础文件名开始检查大小，找到合适的文件
        /// </summary>
        /// <param name="logFolder">日志文件目录</param>
        /// <param name="dateTime">日志日期</param>
        /// <returns>可用的日志文件路径</returns>
        private static string GetAvailableLogFilePath(string logFolder, DateTime dateTime)
        {
            if (MaxLogFileSizeMB <= 0)
            {
                MaxLogFileSizeMB = 500;
            }

            var maxSizeBytes = (long)MaxLogFileSizeMB * 1024 * 1024;
            var baseName = dateTime.ToString("yyyy_MM_dd");

            var logFilePath = Path.Combine(logFolder, $"Log_{baseName}.log");

            if (!File.Exists(logFilePath) || new FileInfo(logFilePath).Length < maxSizeBytes)
            {
                return logFilePath;
            }

            var sequenceNumber = 1;
            while (true)
            {
                logFilePath = Path.Combine(logFolder, $"Log_{baseName}_{sequenceNumber}.log");

                if (!File.Exists(logFilePath) || new FileInfo(logFilePath).Length < maxSizeBytes)
                {
                    return logFilePath;
                }

                sequenceNumber++;
            }
        }
    }
}
