using System;

namespace CodeWF.Log.Core;

public readonly struct LogInfo
{
    public LogInfo(LogType logType, string description, string? friendlyDescription = null, bool log2UI = true, bool log2File = true)
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
        Log2UI = log2UI;
        Log2File = log2File;
    }

    public LogType Level { get; }

    public DateTime RecordTime { get; }

    public string Description { get; }

    public string FriendlyDescription { get; }
    public bool Log2UI { get; } = true;
    public bool Log2File { get; } = true;
}