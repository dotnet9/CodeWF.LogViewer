using System;
using System.ComponentModel;

namespace CodeWF.LogViewer.Avalonia.Log4Net
{
    public interface ILog
    {
        event Action<LogInfo> LogNotifyEvent;
        bool GetLogManagerState(LogType logType);
        void AddLog(LogType logType, object message);
        void AddLog(LogType logType, object message, Exception exception);
        void Debug(object message);
        void Info(object message);
        void Warn(object message);
        void Error(object message);
        void Fatal(object message);
        void AddFormatLog(LogType logType, string format, params object[] args);
        void AddFormatLog(LogType logType, IFormatProvider provider, string format, params object[] args);
        BindingList<LogInfo> GetLogList();
        void Clear();
    }
}