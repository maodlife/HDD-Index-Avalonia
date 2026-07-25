using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public static FileNode? CreateByPathSkippingDeclaredSubtrees(
        string path,
        FileNode currentFileNode,
        IProgress<FileNodeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            return CreateByPath(
                path,
                progress,
                cancellationToken,
                new FileNodeScanProgressState(),
                currentFileNode,
                canSkipDeclaredSubtree: false);
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
        return CreateByPath(
            path,
            progress,
            cancellationToken,
            progressState,
            currentFileNode: null,
            canSkipDeclaredSubtree: false);
    }

    private static FileNode? CreateByPath(
        string path,
        IProgress<FileNodeScanProgress>? progress,
        CancellationToken cancellationToken,
        FileNodeScanProgressState progressState,
        FileNode? currentFileNode,
        bool canSkipDeclaredSubtree)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(progressState.CreateProgress(path));

        if (IsFileHidden(path))
            return null;

        if (canSkipDeclaredSubtree
            && currentFileNode is { IsDirectory: true }
            && currentFileNode.DeclareRepoNodeDatas.Count > 0)
        {
            return CloneSubtree(currentFileNode);
        }

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

        var currentChildrenByName = currentFileNode?.Children
            .OfType<FileNode>()
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> directories =
            topLevelDirectories ?? Directory.EnumerateDirectories(path);
        foreach (var dir in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (topLevelDirectories == null && IsFileHidden(dir))
                continue;

            var childFileName = Path.GetFileName(dir);
            currentChildrenByName.TryGetValue(childFileName, out var currentChild);
            var child = TryCreateChildByPath(
                dir,
                progress,
                cancellationToken,
                progressState,
                currentChild,
                canSkipDeclaredSubtree: true);
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
        FileNodeScanProgressState progressState,
        FileNode? currentFileNode = null,
        bool canSkipDeclaredSubtree = false)
    {
        try
        {
            return CreateByPath(
                path,
                progress,
                cancellationToken,
                progressState,
                currentFileNode,
                canSkipDeclaredSubtree);
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

    private static FileNode CloneSubtree(FileNode source, TreeNodeBase? parent = null)
    {
        var clone = new FileNode
        {
            Name = source.Name,
            IsDirectory = source.IsDirectory,
            Parent = parent,
            DeclareRepoNodeDatas = source.DeclareRepoNodeDatas
                .Select(x => (DeclareRepoNodeData)x.Clone())
                .ToList()
        };

        foreach (var child in source.Children.OfType<FileNode>())
            clone.Children.Add(CloneSubtree(child, clone));

        return clone;
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
    public string RepoNodePath { get; set; } = string.Empty;

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}
