using System;
using System.Collections.Concurrent;

namespace CodeWF.LogViewer.Avalonia
{
    public static class Logger
    {
        public static LogType Level = LogType.Info;
        internal static readonly ConcurrentQueue<LogInfo> Logs = new();

        public static bool TryDequeue(out LogInfo info)
        {
            return Logs.TryDequeue(out info);
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
    }
}