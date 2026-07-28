using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using HDD_Index.Application.FileScanning;
using HDD_Index.Models;

namespace HDD_Index.Services;

public interface IFileSystemReader
{
    IEnumerable<string> EnumerateFiles(string path);
    IEnumerable<string> EnumerateDirectories(string path);
    FileAttributes GetAttributes(string path);
}

public sealed class FileTreeScanner : IFileTreeScanner
{
    private readonly IFileSystemReader _fileSystem;

    public FileTreeScanner()
        : this(new PhysicalFileSystemReader())
    {
    }

    public FileTreeScanner(IFileSystemReader fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public FileTreeScanResult Scan(
        FileTreeScanRequest request,
        IProgress<FileTreeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var issues = new List<FileTreeScanIssue>();
        var progressState = new FileTreeScanProgressState();

        try
        {
            var root = ScanDirectory(
                request.RootPath,
                progress,
                cancellationToken,
                progressState,
                issues,
                request.SkipDeclaredSubtrees ? request.CurrentRoot : null,
                canSkipDeclaredSubtree: false,
                isRoot: true);
            if (root == null)
            {
                if (issues.All(x => x.Severity != FileTreeScanIssueSeverity.Blocking))
                {
                    AddIssue(issues, new FileTreeScanIssue(
                        request.RootPath,
                        FileTreeScanIssueSeverity.Blocking,
                        FileTreeScanIssueKind.HiddenRoot,
                        "扫描根目录是隐藏目录。"));
                }

                return FileTreeScanResult.Failure(
                    FileTreeScanStatus.Failed,
                    issues);
            }

            if (issues.Any(x => x.Severity == FileTreeScanIssueSeverity.Blocking))
            {
                return FileTreeScanResult.Failure(
                    FileTreeScanStatus.PartiallyFailed,
                    issues);
            }

            return FileTreeScanResult.Success(root, issues);
        }
        catch (OperationCanceledException)
        {
            return FileTreeScanResult.Cancelled();
        }
        catch (Exception ex)
        {
            AddIssue(issues, CreateBlockingIssue(request.RootPath, ex));
            return FileTreeScanResult.Failure(
                FileTreeScanStatus.Failed,
                issues);
        }
    }

    private FileNode? ScanDirectory(
        string path,
        IProgress<FileTreeScanProgress>? progress,
        CancellationToken cancellationToken,
        FileTreeScanProgressState progressState,
        ICollection<FileTreeScanIssue> issues,
        FileNode? currentFileNode,
        bool canSkipDeclaredSubtree,
        bool isRoot)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(progressState.CreateProgress(path));

        var inspection = InspectPath(path, isDirectory: true, issues);
        if (inspection == PathInspection.Hidden)
            return null;
        if (inspection == PathInspection.Blocked)
            return null;

        if (canSkipDeclaredSubtree
            && currentFileNode is { IsDirectory: true }
            && currentFileNode.DeclareRepoNodeDatas.Count > 0)
        {
            return CloneSubtree(currentFileNode);
        }

        var fileNode = new FileNode
        {
            Name = Path.GetFileName(path),
            IsDirectory = true,
        };

        List<string>? topLevelFiles = null;
        List<string>? topLevelDirectories = null;
        if (isRoot)
        {
            topLevelFiles = GetVisibleFiles(
                path,
                cancellationToken,
                issues);
            topLevelDirectories = GetVisibleDirectories(
                path,
                cancellationToken,
                issues);
            progressState.TotalTopLevelEntries =
                topLevelFiles.Count + topLevelDirectories.Count;
            progress?.Report(progressState.CreateProgress(path));
        }

        var files = topLevelFiles ?? _fileSystem.EnumerateFiles(path);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(progressState.CreateProgress(file));
            if (topLevelFiles == null
                && InspectPath(file, isDirectory: false, issues)
                    != PathInspection.Visible)
            {
                continue;
            }

            fileNode.Children.Add(new FileNode
            {
                Name = Path.GetFileName(file),
                IsDirectory = false,
            });

            if (isRoot)
                ReportTopLevelEntryCompleted(progress, progressState, file);
        }

        var currentChildrenByName = currentFileNode?.Children
            .OfType<FileNode>()
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);

        var directories =
            topLevelDirectories ?? _fileSystem.EnumerateDirectories(path);
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (topLevelDirectories == null
                && InspectPath(directory, isDirectory: true, issues)
                    != PathInspection.Visible)
            {
                continue;
            }

            var childName = Path.GetFileName(directory);
            currentChildrenByName.TryGetValue(childName, out var currentChild);
            var child = TryScanChildDirectory(
                directory,
                progress,
                cancellationToken,
                progressState,
                issues,
                currentChild);
            if (child != null)
                fileNode.Children.Add(child);

            if (isRoot)
                ReportTopLevelEntryCompleted(progress, progressState, directory);
        }

        return fileNode;
    }

    private FileNode? TryScanChildDirectory(
        string path,
        IProgress<FileTreeScanProgress>? progress,
        CancellationToken cancellationToken,
        FileTreeScanProgressState progressState,
        ICollection<FileTreeScanIssue> issues,
        FileNode? currentFileNode)
    {
        try
        {
            return ScanDirectory(
                path,
                progress,
                cancellationToken,
                progressState,
                issues,
                currentFileNode,
                canSkipDeclaredSubtree: true,
                isRoot: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AddIssue(issues, CreateBlockingIssue(path, ex));
            return null;
        }
    }

    private List<string> GetVisibleFiles(
        string path,
        CancellationToken cancellationToken,
        ICollection<FileTreeScanIssue> issues)
    {
        var result = new List<string>();
        foreach (var file in _fileSystem.EnumerateFiles(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (InspectPath(file, isDirectory: false, issues) == PathInspection.Visible)
                result.Add(file);
        }

        return result;
    }

    private List<string> GetVisibleDirectories(
        string path,
        CancellationToken cancellationToken,
        ICollection<FileTreeScanIssue> issues)
    {
        var result = new List<string>();
        foreach (var directory in _fileSystem.EnumerateDirectories(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (InspectPath(directory, isDirectory: true, issues)
                == PathInspection.Visible)
            {
                result.Add(directory);
            }
        }

        return result;
    }

    private PathInspection InspectPath(
        string path,
        bool isDirectory,
        ICollection<FileTreeScanIssue> issues)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith('.'))
            return PathInspection.Hidden;

        FileAttributes attributes;
        try
        {
            attributes = _fileSystem.GetAttributes(path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsAttributeReadException(ex))
        {
            AddIssue(issues, new FileTreeScanIssue(
                path,
                FileTreeScanIssueSeverity.Warning,
                FileTreeScanIssueKind.AttributeReadFailed,
                BuildIssueMessage("无法读取隐藏属性", ex)));
            return PathInspection.Visible;
        }
        catch (Exception ex)
        {
            AddIssue(issues, new FileTreeScanIssue(
                path,
                FileTreeScanIssueSeverity.Blocking,
                FileTreeScanIssueKind.Unexpected,
                BuildIssueMessage("读取文件属性时发生未知错误", ex)));
            return PathInspection.Blocked;
        }

        if ((attributes & FileAttributes.Hidden) != 0)
            return PathInspection.Hidden;

        if (isDirectory && (attributes & FileAttributes.ReparsePoint) != 0)
        {
            AddIssue(issues, new FileTreeScanIssue(
                path,
                FileTreeScanIssueSeverity.Blocking,
                FileTreeScanIssueKind.DirectoryReparsePoint,
                "不支持扫描目录符号链接或 Windows junction。"));
            return PathInspection.Blocked;
        }

        return PathInspection.Visible;
    }

    private static FileNode CloneSubtree(
        FileNode source,
        TreeNodeBase? parent = null)
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

    private static void ReportTopLevelEntryCompleted(
        IProgress<FileTreeScanProgress>? progress,
        FileTreeScanProgressState progressState,
        string path)
    {
        progressState.CompletedTopLevelEntries++;
        progress?.Report(progressState.CreateProgress(path));
    }

    private static FileTreeScanIssue CreateBlockingIssue(
        string path,
        Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException or SecurityException =>
                new FileTreeScanIssue(
                    path,
                    FileTreeScanIssueSeverity.Blocking,
                    FileTreeScanIssueKind.AccessDenied,
                    BuildIssueMessage("无权限访问", exception)),
            DirectoryNotFoundException =>
                new FileTreeScanIssue(
                    path,
                    FileTreeScanIssueSeverity.Blocking,
                    FileTreeScanIssueKind.DirectoryNotFound,
                    BuildIssueMessage("目录不存在", exception)),
            IOException =>
                new FileTreeScanIssue(
                    path,
                    FileTreeScanIssueSeverity.Blocking,
                    FileTreeScanIssueKind.IoError,
                    BuildIssueMessage("读取目录时发生 I/O 错误", exception)),
            _ =>
                new FileTreeScanIssue(
                    path,
                    FileTreeScanIssueSeverity.Blocking,
                    FileTreeScanIssueKind.Unexpected,
                    BuildIssueMessage("读取目录时发生未知错误", exception))
        };
    }

    private static string BuildIssueMessage(string prefix, Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? prefix
            : $"{prefix}：{exception.Message}";
    }

    private static bool IsAttributeReadException(Exception exception)
    {
        return exception is UnauthorizedAccessException
            or SecurityException
            or IOException;
    }

    private static void AddIssue(
        ICollection<FileTreeScanIssue> issues,
        FileTreeScanIssue issue)
    {
        if (!issues.Contains(issue))
            issues.Add(issue);
    }

    private enum PathInspection
    {
        Visible,
        Hidden,
        Blocked
    }

    private sealed class FileTreeScanProgressState
    {
        public int CompletedTopLevelEntries { get; set; }
        public int? TotalTopLevelEntries { get; set; }

        public FileTreeScanProgress CreateProgress(string currentPath)
        {
            return new FileTreeScanProgress(
                CompletedTopLevelEntries,
                TotalTopLevelEntries ?? 0,
                currentPath);
        }
    }

    private sealed class PhysicalFileSystemReader : IFileSystemReader
    {
        public IEnumerable<string> EnumerateFiles(string path)
        {
            return Directory.EnumerateFiles(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return Directory.EnumerateDirectories(path);
        }

        public FileAttributes GetAttributes(string path)
        {
            return File.GetAttributes(path);
        }
    }
}
