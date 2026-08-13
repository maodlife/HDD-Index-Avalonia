using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HDD_Index.Application.FileScanning;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Application.FileTrees;

public sealed class FileTreeUseCases
{
    private readonly ApplicationSession _session;
    private readonly IFileTreeEditingService _fileTreeEditingService;
    private readonly IFileTreeScanner _fileTreeScanner;
    private readonly IFileTreePathService _pathService;

    public FileTreeUseCases(
        ApplicationSession session,
        IFileTreeEditingService fileTreeEditingService,
        IFileTreeScanner fileTreeScanner,
        IFileTreePathService pathService)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _fileTreeEditingService = fileTreeEditingService
                                  ?? throw new ArgumentNullException(
                                      nameof(fileTreeEditingService));
        _fileTreeScanner = fileTreeScanner
                           ?? throw new ArgumentNullException(nameof(fileTreeScanner));
        _pathService = pathService
                       ?? throw new ArgumentNullException(nameof(pathService));
    }

    public bool AreNodesSynchronized(
        RepoNode? repoNode,
        FileNode? fileNode)
    {
        return _fileTreeEditingService.CheckRepoNodeAndFileNodeIsSync(
            repoNode,
            fileNode);
    }

    public string? GetLocalPath(
        FileData? fileData,
        FileNode fileNode)
    {
        ArgumentNullException.ThrowIfNull(fileNode);

        var localFolderPath = fileData?.LocalFolderPath;
        if (string.IsNullOrWhiteSpace(localFolderPath))
            return null;

        var path = localFolderPath;
        foreach (var segment in fileNode.GetPath()
                     .Split('/', StringSplitOptions.RemoveEmptyEntries)
                     .Skip(1))
        {
            path = _pathService.Combine(path, segment);
        }

        return path;
    }

    public NewFileTreePlan PlanNewFileTree(
        string selectedPath,
        string diskLabel)
    {
        selectedPath = selectedPath.Trim();
        diskLabel = diskLabel.Trim();
        if (string.IsNullOrWhiteSpace(selectedPath)
            || string.IsNullOrWhiteSpace(diskLabel))
        {
            return NewFileTreePlan.Failure("请选择文件夹并填写标签。");
        }

        if (_pathService.ContainsInvalidFileNameChars(diskLabel))
        {
            return NewFileTreePlan.Failure(
                "标签包含不能用于文件名的字符，请修改标签。");
        }

        if (_session.FileDatas.Any(fileData =>
                string.Equals(
                    fileData.DiskLabel,
                    diskLabel,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return NewFileTreePlan.Failure(
                $"已存在标签为 {diskLabel} 的文件树，请修改标签。");
        }

        var relativeJsonFilePath = $"{diskLabel}.json";
        var jsonFilePath = _pathService.Combine(
            _session.AppConfig.JsonFilePath,
            relativeJsonFilePath);
        if (_pathService.FileExists(jsonFilePath))
        {
            return NewFileTreePlan.Failure(
                $"文件树 JSON 已存在：{jsonFilePath}");
        }

        return NewFileTreePlan.Success(
            selectedPath,
            diskLabel,
            relativeJsonFilePath,
            jsonFilePath);
    }

    public FileTreeScanResult ScanNewFileTree(
        NewFileTreePlan plan,
        IProgress<FileTreeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.Succeeded)
        {
            throw new ArgumentException(
                "不能执行未通过验证的新建文件树计划。",
                nameof(plan));
        }

        return _fileTreeScanner.Scan(
            new FileTreeScanRequest(plan.SelectedPath),
            progress,
            cancellationToken);
    }

    public FileTreeOperationResult ApplyNewFileTree(
        NewFileTreePlan plan,
        FileTreeScanResult scanResult)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(scanResult);

        if (!plan.Succeeded)
            return FileTreeOperationResult.Failure(plan.FailureReason);
        if (scanResult.Status != FileTreeScanStatus.Succeeded
            || scanResult.Root == null)
        {
            return FileTreeOperationResult.Failure("文件树扫描未成功，不能创建索引。");
        }

        EnsureAppConfigFileDataFilesInitialized();
        var fileData = new FileData
        {
            DiskLabel = plan.DiskLabel,
            LocalFolderPath = plan.SelectedPath,
            JsonFilePath = plan.JsonFilePath,
            FileNodeRoot = scanResult.Root,
        };
        _session.FileDatas.Add(fileData);
        _session.AppConfig.FileDataFiles.Add(new FileDataFileConfig
        {
            JsonFilePath = plan.RelativeJsonFilePath,
            LocalFolderPath = plan.SelectedPath,
        });

        return FileTreeOperationResult.Success(
            TreeChangeSet.Empty,
            [
                PersistenceTarget.AppConfig,
                PersistenceTarget.ForFileData(plan.DiskLabel),
            ],
            fileData);
    }

    public FileTreeRefreshPlan PlanRefresh(
        FileData fileData,
        FileNode currentFileNode,
        bool skipDeclaredSubtrees)
    {
        ArgumentNullException.ThrowIfNull(fileData);
        ArgumentNullException.ThrowIfNull(currentFileNode);

        if (!currentFileNode.IsDirectory)
        {
            return FileTreeRefreshPlan.Failure(
                fileData,
                currentFileNode,
                skipDeclaredSubtrees,
                "只能刷新目录节点。");
        }

        if (string.IsNullOrWhiteSpace(fileData.DiskLabel))
        {
            return FileTreeRefreshPlan.Failure(
                fileData,
                currentFileNode,
                skipDeclaredSubtrees,
                "磁盘标签不能为空。");
        }

        var localPath = GetLocalPath(fileData, currentFileNode);
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return FileTreeRefreshPlan.Failure(
                fileData,
                currentFileNode,
                skipDeclaredSubtrees,
                "没有配置对应的本地文件夹，无法刷新。");
        }

        return FileTreeRefreshPlan.Success(
            fileData,
            currentFileNode,
            localPath,
            skipDeclaredSubtrees);
    }

    public FileTreeRefreshScanResult ScanRefresh(
        FileTreeRefreshPlan plan,
        IProgress<FileTreeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.Succeeded)
        {
            throw new ArgumentException(
                "不能执行未通过验证的文件树刷新计划。",
                nameof(plan));
        }

        var scanResult = _fileTreeScanner.Scan(
            new FileTreeScanRequest(
                plan.LocalPath,
                plan.CurrentFileNode,
                plan.SkipDeclaredSubtrees),
            progress,
            cancellationToken);
        if (scanResult.Status != FileTreeScanStatus.Succeeded
            || scanResult.Root == null)
        {
            return new FileTreeRefreshScanResult(
                plan,
                scanResult,
                null,
                []);
        }

        var refreshedFileNode = _fileTreeEditingService
            .BuildRefreshedFileNodeSubtree(
                plan.CurrentFileNode,
                scanResult.Root);
        var failures = _fileTreeEditingService
            .GetInvalidDeclareHoldingsAfterRefresh(
                plan.FileData.DiskLabel,
                plan.CurrentFileNode,
                refreshedFileNode)
            .ToArray();

        return new FileTreeRefreshScanResult(
            plan,
            scanResult,
            refreshedFileNode,
            failures);
    }

    public FileTreeOperationResult ApplyRefresh(
        FileTreeRefreshScanResult scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        if (scanResult.FileTreeScan.Status != FileTreeScanStatus.Succeeded
            || scanResult.RefreshedFileNode == null)
        {
            return FileTreeOperationResult.Failure(
                "文件树扫描未成功，不能应用刷新。");
        }

        var diskLabel = scanResult.Plan.FileData.DiskLabel;
        var changes = _fileTreeEditingService.ApplyFileNodeRefresh(
            diskLabel,
            scanResult.Plan.CurrentFileNode,
            scanResult.RefreshedFileNode,
            scanResult.ValidationFailures);
        var persistenceTargets = new List<PersistenceTarget>();
        if (scanResult.ValidationFailures.Count > 0)
            persistenceTargets.Add(PersistenceTarget.Repository);
        persistenceTargets.Add(PersistenceTarget.ForFileData(diskLabel));

        return FileTreeOperationResult.Success(
            changes,
            persistenceTargets);
    }

    public FileTreeOperationResult DeleteFileNode(
        FileData fileData,
        FileNode fileNode)
    {
        ArgumentNullException.ThrowIfNull(fileData);
        ArgumentNullException.ThrowIfNull(fileNode);

        if (string.IsNullOrWhiteSpace(fileData.DiskLabel))
            return FileTreeOperationResult.Failure("磁盘标签不能为空。");

        var result = _fileTreeEditingService.DeleteFileNode(
            fileNode,
            fileData.FileNodeRoot,
            fileData.DiskLabel);
        if (!result.Succeeded)
            return FileTreeOperationResult.Failure(result.FailureReason);

        return FileTreeOperationResult.Success(
            result.Changes,
            [
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData(fileData.DiskLabel),
            ]);
    }

    private void EnsureAppConfigFileDataFilesInitialized()
    {
        if (_session.AppConfig.FileDataFiles.Count > 0)
            return;

        foreach (var fileData in _session.FileDatas)
        {
            if (string.IsNullOrWhiteSpace(fileData.JsonFilePath))
                continue;

            _session.AppConfig.FileDataFiles.Add(new FileDataFileConfig
            {
                JsonFilePath = _pathService.GetRelativePath(
                    _session.AppConfig.JsonFilePath,
                    fileData.JsonFilePath),
                LocalFolderPath = fileData.LocalFolderPath,
            });
        }
    }
}
