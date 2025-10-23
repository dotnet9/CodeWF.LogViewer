using CodeWF.LogViewer.Avalonia.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CodeWF.LogViewer.Avalonia
{
    public static class Logger
    {
        /// <summary>
        /// Gets or sets the current log level used for logging operations.
        /// </summary>
        public static LogType Level = LogType.Info; 
        /// <summary>
        /// Specifies the directory where log files are stored.
        /// </summary>
        /// <remarks>The default value is the application's base directory. Update this field to change
        /// the log file location as needed.</remarks>
        public static string LogDir = AppDomain.CurrentDomain.BaseDirectory; 
        /// <summary>
        /// Specifies the default number of log entries to process in a single batch operation.
        /// </summary>
        public static int BatchProcessSize = 50;  
        /// <summary>
        /// Represents a thread-safe queue of log entries for internal use.
        /// </summary>
        internal static readonly ConcurrentQueue<LogInfo> Logs = new();

        /// <summary>
        /// Starts a background task that continuously processes log entries in batches and writes them to a file.
        /// </summary>
        /// <remarks>This method is intended to be called once during application startup to enable
        /// asynchronous, batched log file writing. It processes log entries from an internal queue, writing them to the
        /// file in groups to improve performance. The method does not block the calling thread and returns immediately.
        /// Multiple invocations may result in multiple background tasks writing logs concurrently, which may not be
        /// intended.</remarks>
        public static void RecordToFile()
        {
            Task.Run(async () =>
            {
                var logsInBatch = new List<LogInfo>();
                var logContentBuilder = new StringBuilder();
                
                while (true)
                {
                    // 收集一批日志
                    logsInBatch.Clear();
                    logContentBuilder.Clear();
                    
                    // 尝试获取指定数量的日志
                    int count = 0;
                    while (count < BatchProcessSize && TryDequeue(out var log))
                    {
                        logsInBatch.Add(log);
                        count++;
                    }
                    
                    // 如果有日志需要写入
                    if (logsInBatch.Count > 0)
                    {
                        // 构建批处理日志内容
                        foreach (var log in logsInBatch)
                        {
                            logContentBuilder.AppendLine($"{log.RecordTime}: {log.Level.Description()} {log.Description}");
                        }
                        
                        // 批量写入文件（只打开和关闭文件一次）
                        AddLogToFile(logContentBuilder.ToString());
                        
                        // 如果队列中还有大量日志，不休眠继续处理
                        if (Logs.Count > 0)
                        {
                            continue;
                        }
                    }
                    
                    // 如果队列为空，适当延长休眠时间减少CPU消耗
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            });
        }

        public static bool TryDequeue(out LogInfo info)
        {
            return Logs.TryDequeue(out info);
        }

        /// <summary>
        /// 查看队列头部的日志但不移除
        /// </summary>
        /// <param name="info">返回队列头部的日志信息</param>
        /// <returns>如果队列不为空则返回true，否则返回false</returns>
        public static bool TryPeek(out LogInfo info)
        {
            return Logs.TryPeek(out info);
        }

        public static void Flush()
        {
            // 批量收集所有日志
            var logsBatch = new System.Collections.Generic.List<LogInfo>();
            while (TryDequeue(out var log))
            {
                logsBatch.Add(log);
            }
            
            // 使用批量写入方法，减少文件I/O操作
            if (logsBatch.Count > 0)
            {
                AddLogBatchToFile(logsBatch);
            }
        }

        public static void Log(int type, string content, string? friendlyContent = default)
        {
            var logType = (LogType)type;
            if (Level > logType) return;
            Logs.Enqueue(new LogInfo(logType, content, friendlyContent));
        }

        public static void Log(LogType type, string content, string? friendlyContent = default)
        {
            if (Level > type) return;
            Logs.Enqueue(new LogInfo(type, content, friendlyContent));
        }

        public static void Debug(string content, string? friendlyContent = default)
        {
            if (Level <= LogType.Debug)
            {
                Logs.Enqueue(new LogInfo(LogType.Debug, content, friendlyContent));
            }
        }

        public static void Info(string content, string? friendlyContent = default)
        {
            if (Level <= LogType.Info)
            {
                Logs.Enqueue(new LogInfo(LogType.Info, content, friendlyContent));
            }
        }

        public static void Warn(string content, string? friendlyContent = default)
        {
            if (Level <= LogType.Warn)
            {
                Logs.Enqueue(new LogInfo(LogType.Warn, content, friendlyContent));
            }
        }

        public static void Error(string content, Exception? ex = null, string? friendlyContent = default)
        {
            if (Level > LogType.Error) return;

            var msg = ex == null ? content : $"{content}\r\n{ex.ToString()}";

            Logs.Enqueue(new LogInfo(LogType.Error, msg, friendlyContent));
        }

        public static void Fatal(string content, Exception? ex = null, string? friendlyContent = default)
        {
            if (Level > LogType.Fatal) return;

            var msg = ex == null ? content : $"{content}\r\n{ex.ToString()}";

            Logs.Enqueue(new LogInfo(LogType.Fatal, msg, friendlyContent));
        }

        public static void AddLogToFile(LogInfo logInfo)
        {
            AddLogToFile(
                $"{logInfo.RecordTime}: {logInfo.Level.Description()} {logInfo.Description}{Environment.NewLine}");
        }
        
        /// <summary>
        /// 批量将日志写入文件，提高大量日志写入时的性能
        /// </summary>
        /// <param name="logsBatch">日志批次</param>
        public static void AddLogBatchToFile(List<LogInfo> logsBatch)
        {
            if (logsBatch == null || logsBatch.Count == 0)
                return;
                
            try
            {
                // 使用StringBuilder批量构建日志内容，减少字符串拼接开销
                var logContentBuilder = new StringBuilder();
                
                foreach (var logInfo in logsBatch)
                {
                    logContentBuilder.AppendLine($"{logInfo.RecordTime}: {logInfo.Level.Description()} {logInfo.Description}");
                }
                
                // 只调用一次文件写入，大幅减少I/O操作
                AddLogToFile(logContentBuilder.ToString());
            }
            catch
            {
                // ignored
            }
        }

        public static void AddLogToFile(string msg)
        {
            try
            {
                var logFolder = System.IO.Path.Combine(LogDir, "Log");
                // 目录检查可以移到RecordToFile方法中，但保留在这里以确保安全
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                var logFileName = System.IO.Path.Combine(logFolder, $"Log_{DateTime.Now:yyyy_MM_dd}.log");
                // 使用File.AppendAllText对于批量内容仍然有效，但可以考虑更高级的文件写入方式
                // 对于批量写入，这种方式已经比单条写入效率高很多
                File.AppendAllText(logFileName, msg);
            }
            catch
            {
                // ignored
            }
        }
    }
}