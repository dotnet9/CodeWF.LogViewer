using System;

namespace CodeWF.LogViewer.Avalonia.Log4Net
{
    public readonly struct LogInfo
    {
        public LogInfo(LogType logType, string description)
        {
            RecordTime = DateTime.Now;
            Level = logType;
            Description = description;
        }

        public LogType Level { get; }

        public DateTime RecordTime { get; }

        public string Description { get; }
    }
}