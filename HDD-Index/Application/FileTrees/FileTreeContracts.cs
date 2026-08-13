using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Declarations;
using HDD_Index.Application.FileScanning;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Application.FileTrees;

public interface IFileTreeEditingService
{
    bool CheckRepoNodeAndFileNodeIsSync(
        RepoNode? repoNode,
        FileNode? fileNode);

    TreeEditResult<FileNode> DeleteFileNode(
        FileNode fileNode,
        FileNode fileNodeRoot,
        string diskLabel);

    FileNode BuildRefreshedFileNodeSubtree(
        FileNode currentFileNode,
        FileNode scannedFileNode);

    IReadOnlyList<DeclareHoldingValidationFailure>
        GetInvalidDeclareHoldingsAfterRefresh(
            string diskLabel,
            FileNode currentFileNode,
            FileNode refreshedFileNode);

    TreeChangeSet ApplyFileNodeRefresh(
        string diskLabel,
        FileNode currentFileNode,
        FileNode refreshedFileNode,
        IEnumerable<DeclareHoldingValidationFailure> failuresToRemove);
}

public interface IFileTreePathService
{
    bool ContainsInvalidFileNameChars(string fileName);

    bool FileExists(string path);

    string Combine(string firstPath, string secondPath);

    string GetRelativePath(string relativeTo, string path);
}

public sealed record NewFileTreePlan(
    string SelectedPath,
    string DiskLabel,
    string RelativeJsonFilePath,
    string JsonFilePath,
    string FailureReason)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(FailureReason);

    public static NewFileTreePlan Success(
        string selectedPath,
        string diskLabel,
        string relativeJsonFilePath,
        string jsonFilePath)
    {
        return new NewFileTreePlan(
            selectedPath,
            diskLabel,
            relativeJsonFilePath,
            jsonFilePath,
            string.Empty);
    }

    public static NewFileTreePlan Failure(string failureReason)
    {
        return new NewFileTreePlan(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            failureReason);
    }
}

public sealed record FileTreeRefreshPlan(
    FileData FileData,
    FileNode CurrentFileNode,
    string LocalPath,
    bool SkipDeclaredSubtrees,
    string FailureReason)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(FailureReason);

    public static FileTreeRefreshPlan Success(
        FileData fileData,
        FileNode currentFileNode,
        string localPath,
        bool skipDeclaredSubtrees)
    {
        return new FileTreeRefreshPlan(
            fileData,
            currentFileNode,
            localPath,
            skipDeclaredSubtrees,
            string.Empty);
    }

    public static FileTreeRefreshPlan Failure(
        FileData fileData,
        FileNode currentFileNode,
        bool skipDeclaredSubtrees,
        string failureReason)
    {
        return new FileTreeRefreshPlan(
            fileData,
            currentFileNode,
            string.Empty,
            skipDeclaredSubtrees,
            failureReason);
    }
}

public sealed record FileTreeRefreshScanResult(
    FileTreeRefreshPlan Plan,
    FileTreeScanResult FileTreeScan,
    FileNode? RefreshedFileNode,
    IReadOnlyList<DeclareHoldingValidationFailure> ValidationFailures);

public sealed record FileTreeOperationResult(
    bool Succeeded,
    string FailureReason,
    TreeChangeSet Changes,
    IReadOnlyList<PersistenceTarget> PersistenceTargets,
    FileData? AddedFileData)
{
    public static FileTreeOperationResult Success(
        TreeChangeSet changes,
        IEnumerable<PersistenceTarget> persistenceTargets,
        FileData? addedFileData = null)
    {
        return new FileTreeOperationResult(
            true,
            string.Empty,
            changes,
            persistenceTargets.Distinct().ToArray(),
            addedFileData);
    }

    public static FileTreeOperationResult Failure(string failureReason)
    {
        return new FileTreeOperationResult(
            false,
            failureReason,
            TreeChangeSet.Empty,
            [],
            null);
    }
}
