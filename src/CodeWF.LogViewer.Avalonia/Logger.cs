using CodeWF.LogViewer.Avalonia.Extensions;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace CodeWF.LogViewer.Avalonia
{
    public static class Logger
    {
        public static LogType Level = LogType.Info;
        public static string LogDir = AppDomain.CurrentDomain.BaseDirectory;
        internal static readonly ConcurrentQueue<LogInfo> Logs = new();

        public static void RecordToFile()
        {
            Task.Run(async () =>
            {
                var batchSize = 100; // 每批处理的日志数量
                var logsInBatch = new System.Collections.Generic.List<LogInfo>();
                var logContentBuilder = new System.Text.StringBuilder();
                
                while (true)
                {
                    // 收集一批日志
                    logsInBatch.Clear();
                    logContentBuilder.Clear();
                    
                    // 尝试获取指定数量的日志
                    int count = 0;
                    while (count < batchSize && TryDequeue(out var log))
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
            while (TryDequeue(out var log)) AddLogToFile(log);
        }

        public static void Log(int type, string content)
        {
            var logType = (LogType)type;
            if (Level > logType) return;
            Logs.Enqueue(new LogInfo(logType, content));
        }

        public static void Log(LogType type, string content)
        {
            if (Level > type) return;
            Logs.Enqueue(new LogInfo(type, content));
        }

        public static void Debug(string content)
        {
            if (Level <= LogType.Debug)
            {
                Logs.Enqueue(new LogInfo(LogType.Debug, content));
            }
        }

        public static void Info(string content)
        {
            if (Level <= LogType.Info)
            {
                Logs.Enqueue(new LogInfo(LogType.Info, content));
            }
        }

        public static void Warn(string content)
        {
            if (Level <= LogType.Warn)
            {
                Logs.Enqueue(new LogInfo(LogType.Warn, content));
            }
        }

        public static void Error(string content, Exception? ex = null)
        {
            if (Level > LogType.Error) return;

            var msg = ex == null ? content : $"{content}\r\n{ex.ToString()}";

            Logs.Enqueue(new LogInfo(LogType.Error, msg));
        }

        public static void Fatal(string content, Exception? ex = null)
        {
            if (Level > LogType.Fatal) return;

            var msg = ex == null ? content : $"{content}\r\n{ex.ToString()}";

            Logs.Enqueue(new LogInfo(LogType.Fatal, msg));
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
        public static void AddLogBatchToFile(System.Collections.Generic.List<LogInfo> logsBatch)
        {
            if (logsBatch == null || logsBatch.Count == 0)
                return;
                
            try
            {
                // 使用StringBuilder批量构建日志内容，减少字符串拼接开销
                var logContentBuilder = new System.Text.StringBuilder();
                
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