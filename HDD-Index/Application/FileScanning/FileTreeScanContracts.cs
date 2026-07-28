using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HDD_Index.Models;

namespace HDD_Index.Application.FileScanning;

public interface IFileTreeScanner
{
    FileTreeScanResult Scan(
        FileTreeScanRequest request,
        IProgress<FileTreeScanProgress>? progress,
        CancellationToken cancellationToken);
}

public sealed record FileTreeScanRequest(
    string RootPath,
    FileNode? CurrentRoot = null,
    bool SkipDeclaredSubtrees = false);

public sealed record FileTreeScanProgress(
    int CompletedTopLevelEntries,
    int TotalTopLevelEntries,
    string CurrentPath);

public enum FileTreeScanStatus
{
    Succeeded,
    Cancelled,
    Failed,
    PartiallyFailed
}

public enum FileTreeScanIssueSeverity
{
    Blocking,
    Warning
}

public enum FileTreeScanIssueKind
{
    AccessDenied,
    DirectoryNotFound,
    IoError,
    AttributeReadFailed,
    DirectoryReparsePoint,
    HiddenRoot,
    Unexpected
}

public sealed record FileTreeScanIssue(
    string Path,
    FileTreeScanIssueSeverity Severity,
    FileTreeScanIssueKind Kind,
    string Message);

public sealed class FileTreeScanResult
{
    public FileTreeScanStatus Status { get; }
    public FileNode? Root { get; }
    public IReadOnlyList<FileTreeScanIssue> Issues { get; }

    public IReadOnlyList<FileTreeScanIssue> BlockingIssues =>
        Issues.Where(x => x.Severity == FileTreeScanIssueSeverity.Blocking).ToList();

    public IReadOnlyList<FileTreeScanIssue> Warnings =>
        Issues.Where(x => x.Severity == FileTreeScanIssueSeverity.Warning).ToList();

    private FileTreeScanResult(
        FileTreeScanStatus status,
        FileNode? root,
        IReadOnlyList<FileTreeScanIssue> issues)
    {
        Status = status;
        Root = root;
        Issues = issues.ToArray();
    }

    public static FileTreeScanResult Success(
        FileNode root,
        IReadOnlyList<FileTreeScanIssue> issues)
    {
        return new FileTreeScanResult(
            FileTreeScanStatus.Succeeded,
            root,
            issues);
    }

    public static FileTreeScanResult Cancelled()
    {
        return new FileTreeScanResult(
            FileTreeScanStatus.Cancelled,
            root: null,
            Array.Empty<FileTreeScanIssue>());
    }

    public static FileTreeScanResult Failure(
        FileTreeScanStatus status,
        IReadOnlyList<FileTreeScanIssue> issues)
    {
        if (status is not FileTreeScanStatus.Failed
            and not FileTreeScanStatus.PartiallyFailed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "扫描失败结果必须使用 Failed 或 PartiallyFailed 状态。");
        }

        return new FileTreeScanResult(status, root: null, issues);
    }
}
