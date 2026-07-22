using CodeWF.Log.Core;
using CodeWF.Tools.FileExtensions;
using System.IO;

namespace CodeWF.LogViewer.Avalonia.Platform;

internal static class LogFolderLauncher
{
    public static string LogFolder => Path.Combine(Logger.LogDir, "Log");

    public static void Open()
    {
        var logFolder = LogFolder;
        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }

        FileHelper.OpenFolder(logFolder);
    }
}
