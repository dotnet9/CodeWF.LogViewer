using System;

namespace CodeWF.Log.Core;

public readonly struct LogInfo
{
    public LogInfo(LogType logType, string description, string? friendlyDescription)
    {
        RecordTime = DateTime.Now;
        Level = logType;
        Description = description;
        if (string.IsNullOrWhiteSpace(friendlyDescription))
        {
            friendlyDescription = description;
        }
        else
        {
            FriendlyDescription = friendlyDescription;
        }
    }

    public LogType Level { get; }

    public DateTime RecordTime { get; }

    public string Description { get; }

    public string FriendlyDescription { get; }
}