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
                while (true)
                {
                    while (TryDequeue(out var log))
                    {
                        var content =
                            $"{log.RecordTime}: {log.Level.Description()} {log.Description}{Environment.NewLine}";
                        AddLogToFile(content);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            });
        }

        public static bool TryDequeue(out LogInfo info)
        {
            return Logs.TryDequeue(out info);
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

        public static void AddLogToFile(string msg)
        {
            try
            {
                var logFolder = System.IO.Path.Combine(LogDir, "Log");
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                var logFileName = System.IO.Path.Combine(logFolder, $"Log_{DateTime.Now:yyyy_MM_dd}.log");
                File.AppendAllText(logFileName, msg);
            }
            catch
            {
                // ignored
            }
        }
    }
}