using System;
using System.Diagnostics;
using System.IO;
using HDD_Index.Application.ExternalInteractions;

namespace HDD_Index.Adapters;

public sealed class WindowsExplorerPathOpener : IPathOpener
{
    public void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            LogMissingPath("本地文件夹不存在", folderPath);
            return;
        }

        StartExplorer($"\"{folderPath}\"");
    }

    public void ShowPathInFolder(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            LogMissingPath("本地路径不存在", path);
            return;
        }

        StartExplorer($"/select,\"{path}\"");
    }

    private static void StartExplorer(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true,
        });
    }

    private static void LogMissingPath(string message, string path)
    {
        Debug.WriteLine($"{message}: {path}");
        Console.WriteLine($"{message}: {path}");
    }
}
