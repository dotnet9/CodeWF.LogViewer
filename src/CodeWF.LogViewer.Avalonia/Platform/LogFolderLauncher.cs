using CodeWF.Log.Core;
using CodeWF.Tools.FileExtensions;
using System;
using System.IO;

namespace CodeWF.LogViewer.Avalonia.Platform;

internal static class LogFolderLauncher
{
    public static string LogFolder => Logger.LogDirectory
        ?? throw new InvalidOperationException("当前程序未启用文件日志。");

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
