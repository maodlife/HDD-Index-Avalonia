using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text.Json;

namespace HDD_Index.Models;

/// <summary>
/// 磁盘文件的节点数据结构
/// </summary>
public class FileNode : TreeNodeBase
{
    /// <summary>
    /// 设计上允许一个磁盘文件声明持有多个Repository中的节点，
    /// 从而方便在Repository中整理分类
    /// </summary>
    public List<DeclareRepoNodeData> DeclareRepoNodeDatas { get; set; } =
        new List<DeclareRepoNodeData>();
    
    public static FileNode? CreateByJson(string json)
    {
        var root = JsonSerializer.Deserialize<FileNode>(json);
        root?.SetParent();
        return root;
    }

    public static FileNode? CreateByPath(string path)
    {
        return CreateByPath(path, null, CancellationToken.None);
    }

    public static FileNode? CreateByPath(
        string path,
        IProgress<FileNodeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            return CreateByPath(
                path,
                progress,
                cancellationToken,
                new FileNodeScanProgressState());
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"无权限访问: {path}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"遍历出错: {ex.Message}");
        }
        
        return null;
    }

    private static FileNode? CreateByPath(
        string path,
        IProgress<FileNodeScanProgress>? progress,
        CancellationToken cancellationToken,
        FileNodeScanProgressState progressState)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(progressState.CreateProgress(path));

        if (IsFileHidden(path))
            return null;

        var fileName = Path.GetFileName(path);
        var fileNode = new FileNode()
        {
            Name = fileName,
            IsDirectory = true,
        };

        var isRoot = progressState.TotalTopLevelEntries == null;
        List<string>? topLevelFiles = null;
        List<string>? topLevelDirectories = null;
        if (isRoot)
        {
            topLevelFiles = GetVisibleFiles(path, cancellationToken);
            topLevelDirectories = GetVisibleDirectories(path, cancellationToken);
            progressState.TotalTopLevelEntries =
                topLevelFiles.Count + topLevelDirectories.Count;
            progress?.Report(progressState.CreateProgress(path));
        }

        IEnumerable<string> files = topLevelFiles ?? Directory.EnumerateFiles(path);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(progressState.CreateProgress(file));
            if (topLevelFiles == null && IsFileHidden(file))
                continue;

            var childFileName = Path.GetFileName(file);
            var childFileNode = new FileNode()
            {
                Name = childFileName,
                IsDirectory = false,
            };
            fileNode.Children.Add(childFileNode);

            if (isRoot)
                ReportTopLevelEntryCompleted(progress, progressState, file);
        }

        IEnumerable<string> directories =
            topLevelDirectories ?? Directory.EnumerateDirectories(path);
        foreach (var dir in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (topLevelDirectories == null && IsFileHidden(dir))
                continue;

            var child = TryCreateChildByPath(
                dir,
                progress,
                cancellationToken,
                progressState);
            if (child != null)
                fileNode.Children.Add(child);

            if (isRoot)
                ReportTopLevelEntryCompleted(progress, progressState, dir);
        }

        return fileNode;
    }

    private static FileNode? TryCreateChildByPath(
        string path,
        IProgress<FileNodeScanProgress>? progress,
        CancellationToken cancellationToken,
        FileNodeScanProgressState progressState)
    {
        try
        {
            return CreateByPath(path, progress, cancellationToken, progressState);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"无权限访问: {path}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"遍历出错: {ex.Message}");
        }

        return null;
    }

    private static List<string> GetVisibleFiles(
        string path,
        CancellationToken cancellationToken)
    {
        var result = new List<string>();
        foreach (var file in Directory.EnumerateFiles(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsFileHidden(file))
                result.Add(file);
        }

        return result;
    }

    private static List<string> GetVisibleDirectories(
        string path,
        CancellationToken cancellationToken)
    {
        var result = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsFileHidden(dir))
                result.Add(dir);
        }

        return result;
    }

    private static void ReportTopLevelEntryCompleted(
        IProgress<FileNodeScanProgress>? progress,
        FileNodeScanProgressState progressState,
        string path)
    {
        progressState.CompletedTopLevelEntries++;
        progress?.Report(progressState.CreateProgress(path));
    }
    
    private static bool IsFileHidden(string path)
    {
        string name = Path.GetFileName(path);
        if (name.StartsWith('.')) // macOS/Linux 风格
            return true;

        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Hidden) != 0) // Windows 风格
                return true;
        }
        catch
        {
            // 无法访问文件属性时忽略
        }

        return false;
    }
}

public sealed record FileNodeScanProgress(
    int CompletedTopLevelEntries,
    int TotalTopLevelEntries,
    string CurrentPath);

internal sealed class FileNodeScanProgressState
{
    public int CompletedTopLevelEntries { get; set; }
    public int? TotalTopLevelEntries { get; set; }

    public FileNodeScanProgress CreateProgress(string currentPath)
    {
        return new FileNodeScanProgress(
            CompletedTopLevelEntries,
            TotalTopLevelEntries ?? 0,
            currentPath);
    }
}

/// <summary>
/// 存储这个磁盘文件节点对应声明持有了哪个Repository中的节点
/// </summary>
public class DeclareRepoNodeData : ICloneable
{
    public string RepoNodePath { get; set; }
    
    public object Clone()
    {
        return this.MemberwiseClone();
    }
}