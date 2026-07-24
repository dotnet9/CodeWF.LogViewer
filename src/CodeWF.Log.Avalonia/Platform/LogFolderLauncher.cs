using CodeWF.Log.Core;
using CodeWF.Tools.FileExtensions;
using System;
using System.IO;

namespace CodeWF.Log.Avalonia.Platform;

internal static class LogFolderLauncher
{
    public static bool Open(string? logFolder)
    {
        if (string.IsNullOrWhiteSpace(logFolder)) return false;
        logFolder = Path.GetFullPath(logFolder);
        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }

        FileHelper.OpenFolder(logFolder);
        return true;
    }
}
