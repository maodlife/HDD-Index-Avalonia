using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Services;

public class DeclarationSyncService
{
    private readonly RepoNode _repoNodeRoot;
    private readonly RepoNodeVM _repoNodeVmRoot;
    private readonly IList<FileDataVMBundle> _fileDataVmBundles;

    public DeclarationSyncService(
        RepoNode repoNodeRoot,
        RepoNodeVM repoNodeVmRoot,
        IList<FileDataVMBundle> fileDataVmBundles)
    {
        _repoNodeRoot = repoNodeRoot;
        _repoNodeVmRoot = repoNodeVmRoot;
        _fileDataVmBundles = fileDataVmBundles;
    }

    public bool CheckRepoNodeAndFileNodeIsSync(
        RepoNode? repoNode,
        FileNode? fileNode)
    {
        if (repoNode == null && fileNode == null)
            return true;
        if (repoNode == null || fileNode == null)
            return false;
        if (fileNode.DeclareRepoNodeDatas.Count == 0)
            return false;

        foreach (var declareRepoNodeData in fileNode.DeclareRepoNodeDatas)
        {
            var foundRepoNode = TreeNodeUtils.GetNodeByPathFromRoot(
                _repoNodeRoot,
                declareRepoNodeData.RepoNodePath);
            if (repoNode == foundRepoNode)
                return true;
        }

        return false;
    }

    public bool TryDeclareHolding(
        RepoNode repoNode,
        RepoNodeVM repoNodeVm,
        FileNode fileNode,
        FileNodeVM fileNodeVm,
        string diskLabel,
        DeclareHoldingStrategyType strategyType,
        bool saveStrategyToRepoNode,
        out string failureReason)
    {
        var strategy = DeclareHoldingStrategyFactory.Create(strategyType);
        if (!strategy.CheckDeclareHolding(repoNode, fileNode, out failureReason))
            return false;

        if (saveStrategyToRepoNode)
        {
            repoNode.DeclareHoldingStrategyType = strategyType;
            repoNodeVm.RefreshDeclareHoldingStrategyName();
        }

        var fileNodePath = fileNode.GetPath();
        var repoNodePath = repoNode.GetPath();

        var saveDataExists = repoNode.SaveFileNodeDatas.Any(
            d => d.DiskLabel == diskLabel && d.FileNodePath == fileNodePath);
        if (!saveDataExists)
        {
            repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
            {
                DiskLabel = diskLabel,
                FileNodePath = fileNodePath
            });
        }

        var vmSaveDataExists = repoNodeVm.SaveFileNodeDatas.Any(
            d => d.DiskLabel == diskLabel && d.FileNodePath == fileNodePath);
        if (!vmSaveDataExists)
        {
            repoNodeVm.SaveFileNodeDatas.Add(new SaveFileNodeData
            {
                DiskLabel = diskLabel,
                FileNodePath = fileNodePath
            });
        }

        var declareDataExists = fileNode.DeclareRepoNodeDatas.Any(
            d => d.RepoNodePath == repoNodePath);
        if (!declareDataExists)
        {
            fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
            {
                RepoNodePath = repoNodePath
            });
        }

        var vmDeclareDataExists = fileNodeVm.DeclareRepoNodeDatas.Any(
            d => d.RepoNodePath == repoNodePath);
        if (!vmDeclareDataExists)
        {
            fileNodeVm.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
            {
                RepoNodePath = repoNodePath
            });
        }

        failureReason = string.Empty;
        return true;
    }

    public IReadOnlyList<DeclareHoldingValidationFailure> GetInvalidSaveFileNodeDatasForStrategy(
        RepoNode repoNode,
        DeclareHoldingStrategyType? strategyType)
    {
        var strategy = DeclareHoldingStrategyFactory.Create(
            strategyType ?? DeclareHoldingStrategyType.Default);
        var failures = new List<DeclareHoldingValidationFailure>();

        foreach (var saveData in repoNode.SaveFileNodeDatas)
        {
            var fileNode = FindFileNode(saveData.DiskLabel, saveData.FileNodePath);
            if (fileNode == null)
            {
                failures.Add(new DeclareHoldingValidationFailure(
                    saveData.DiskLabel,
                    saveData.FileNodePath,
                    "找不到对应的 FileNode。"));
                continue;
            }

            if (!strategy.CheckDeclareHolding(repoNode, fileNode, out var failureReason))
            {
                failures.Add(new DeclareHoldingValidationFailure(
                    saveData.DiskLabel,
                    saveData.FileNodePath,
                    failureReason));
            }
        }

        return failures;
    }

    public void ApplyDeclareHoldingStrategy(
        RepoNode repoNode,
        DeclareHoldingStrategyType? strategyType,
        IEnumerable<DeclareHoldingValidationFailure> failuresToRemove)
    {
        repoNode.DeclareHoldingStrategyType = strategyType;

        foreach (var failure in failuresToRemove.ToList())
        {
            RemoveDeclareHolding(
                repoNode,
                failure.DiskLabel,
                failure.FileNodePath);
        }

        var repoNodeVm = TreeNavigationService.FindRepoNodeVmByPath(
            _repoNodeVmRoot,
            repoNode.GetPath(),
            out _);
        repoNodeVm?.RefreshDeclareHoldingStrategyName();
    }

    public FileNode BuildRefreshedFileNodeSubtree(
        FileNode currentFileNode,
        FileNode scannedFileNode)
    {
        var refreshedFileNode = BuildRefreshedFileNodeSubtree(
            currentFileNode,
            scannedFileNode,
            currentFileNode.Parent);
        refreshedFileNode.Name = currentFileNode.Name;
        refreshedFileNode.IsDirectory = currentFileNode.IsDirectory;
        return refreshedFileNode;
    }

    public IReadOnlyList<DeclareHoldingValidationFailure> GetInvalidDeclareHoldingsAfterRefresh(
        string diskLabel,
        FileNode currentFileNode,
        FileNode refreshedFileNode)
    {
        var failures = new List<DeclareHoldingValidationFailure>();
        var seen = new HashSet<(string RepoPath, string FilePath)>();
        var currentPath = currentFileNode.GetPath();
        var currentPathPrefix = currentPath + "/";

        void AddFailure(string repoPath, string filePath, string failureReason)
        {
            if (!seen.Add((repoPath, filePath)))
                return;

            failures.Add(new DeclareHoldingValidationFailure(
                diskLabel,
                filePath,
                failureReason,
                repoPath));
        }

        void CheckFileNodeDeclarations(FileNode fileNode)
        {
            foreach (var declareData in fileNode.DeclareRepoNodeDatas)
            {
                if (string.IsNullOrWhiteSpace(declareData.RepoNodePath))
                    continue;

                var repoNode = TreeNodeUtils.GetNodeByPathFromRoot(
                    _repoNodeRoot,
                    declareData.RepoNodePath) as RepoNode;
                var filePath = fileNode.GetPath();
                if (repoNode == null)
                {
                    AddFailure(
                        declareData.RepoNodePath,
                        filePath,
                        "找不到对应的 RepoNode。");
                    continue;
                }

                if (!TreeNodeUtils.CheckDeclarationStatus(repoNode, fileNode))
                {
                    AddFailure(
                        declareData.RepoNodePath,
                        filePath,
                        "刷新后的 FileNode 不再满足声明持有策略。");
                }
            }

            foreach (var child in fileNode.Children.OfType<FileNode>())
                CheckFileNodeDeclarations(child);
        }

        CheckFileNodeDeclarations(refreshedFileNode);

        foreach (var repoNode in EnumerateRepoNodes(_repoNodeRoot))
        {
            var repoPath = repoNode.GetPath();
            foreach (var saveData in repoNode.SaveFileNodeDatas.ToList())
            {
                if (saveData.DiskLabel != diskLabel
                    || !IsPathInSubtree(saveData.FileNodePath, currentPath, currentPathPrefix))
                {
                    continue;
                }

                var refreshedTarget = FindNodeInRefreshedSubtree(
                    refreshedFileNode,
                    currentPath,
                    saveData.FileNodePath);
                if (refreshedTarget == null)
                {
                    AddFailure(
                        repoPath,
                        saveData.FileNodePath,
                        "刷新后的文件树中找不到对应的 FileNode。");
                    continue;
                }

                var hasDeclaration = refreshedTarget.DeclareRepoNodeDatas
                    .Any(d => d.RepoNodePath == repoPath);
                if (!hasDeclaration)
                {
                    AddFailure(
                        repoPath,
                        saveData.FileNodePath,
                        "刷新后的 FileNode 缺少对应的声明持有数据。");
                }
            }
        }

        return failures;
    }

    public void ApplyFileNodeRefresh(
        string diskLabel,
        FileNode currentFileNode,
        FileNodeVM currentFileNodeVm,
        FileNode refreshedFileNode,
        IEnumerable<DeclareHoldingValidationFailure> failuresToRemove)
    {
        currentFileNode.Name = refreshedFileNode.Name;
        currentFileNode.IsDirectory = refreshedFileNode.IsDirectory;
        currentFileNode.DeclareRepoNodeDatas = refreshedFileNode.DeclareRepoNodeDatas
            .Select(x => (DeclareRepoNodeData)x.Clone())
            .ToList();
        currentFileNode.Children.Clear();
        foreach (var child in refreshedFileNode.Children.OfType<FileNode>())
        {
            child.Parent = currentFileNode;
            currentFileNode.Children.Add(child);
        }

        currentFileNodeVm.Name = currentFileNode.Name;
        currentFileNodeVm.IsDirectory = currentFileNode.IsDirectory;
        currentFileNodeVm.FileNode = currentFileNode;
        currentFileNodeVm.DeclareRepoNodeDatas.Clear();
        foreach (var declareData in currentFileNode.DeclareRepoNodeDatas)
        {
            currentFileNodeVm.DeclareRepoNodeDatas.Add(
                (DeclareRepoNodeData)declareData.Clone());
        }

        currentFileNodeVm.Children.Clear();
        foreach (var child in currentFileNode.Children.OfType<FileNode>())
            currentFileNodeVm.Children.Add(FileNodeVM.Create(child));

        foreach (var failure in failuresToRemove)
            RemoveDeclareHolding(failure.RepoNodePath, diskLabel, failure.FileNodePath);
    }

    public void AbandonDeclareHoldings(
        FileNode fileNode,
        string diskLabel,
        IEnumerable<string> repoNodePaths)
    {
        if (string.IsNullOrWhiteSpace(diskLabel))
            return;

        var fileNodePath = fileNode.GetPath();
        foreach (var repoNodePath in repoNodePaths
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct())
        {
            var declareData = fileNode.DeclareRepoNodeDatas
                .FirstOrDefault(d => d.RepoNodePath == repoNodePath);
            if (declareData != null)
                fileNode.DeclareRepoNodeDatas.Remove(declareData);

            var repoNode = TreeNodeUtils.GetNodeByPathFromRoot(
                _repoNodeRoot,
                repoNodePath) as RepoNode;
            if (repoNode != null)
                RemoveSaveFileNodeData(repoNode, diskLabel, fileNodePath);

            RemoveDeclareDataFromFileNodeVm(diskLabel, fileNodePath, repoNodePath);
        }
    }

    /// <summary>
    /// 删除 FileNode 子树前，先移除这些节点上记录的声明持有关系，
    /// 以同步清理 RepoNode 里的 SaveFileNodeData。
    /// </summary>
    public void RemoveDeclareHoldingsFromFileNodes(
        string diskLabel,
        IEnumerable<FileNode> fileNodes)
    {
        if (string.IsNullOrWhiteSpace(diskLabel))
            return;

        foreach (var fileNode in fileNodes.Distinct().ToList())
        {
            var fileNodePath = fileNode.GetPath();
            var repoNodePaths = fileNode.DeclareRepoNodeDatas
                .Select(x => x.RepoNodePath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            foreach (var repoNodePath in repoNodePaths)
                RemoveDeclareHolding(repoNodePath, diskLabel, fileNodePath);
        }
    }

    /// <summary>
    /// FileNode 子树变更后重新检查仍存在节点上的声明持有关系，
    /// 不再满足策略或指向缺失 RepoNode 的关系会被双向移除。
    /// </summary>
    public void UpdateFileNodeDeclarations(
        string diskLabel,
        IEnumerable<FileNode> fileNodes)
    {
        if (string.IsNullOrWhiteSpace(diskLabel))
            return;

        foreach (var fileNode in fileNodes.Distinct().ToList())
        {
            var fileNodePath = fileNode.GetPath();
            foreach (var declareData in fileNode.DeclareRepoNodeDatas.ToList())
            {
                var repoNodePath = declareData.RepoNodePath;
                if (string.IsNullOrWhiteSpace(repoNodePath))
                {
                    fileNode.DeclareRepoNodeDatas.Remove(declareData);
                    RemoveDeclareDataFromFileNodeVm(diskLabel, fileNodePath, repoNodePath);
                    continue;
                }

                var repoNode = TreeNodeUtils.GetNodeByPathFromRoot(
                    _repoNodeRoot,
                    repoNodePath) as RepoNode;
                if (repoNode != null
                    && TreeNodeUtils.CheckDeclarationStatus(repoNode, fileNode))
                {
                    continue;
                }

                RemoveDeclareHolding(repoNodePath, diskLabel, fileNodePath);
            }
        }
    }

    public void TryEstablishSaveFileNodeDatasForNode(RepoNode node)
    {
        var parent = node.Parent as RepoNode;
        if (parent == null || !parent.SaveFileNodeDatas.Any())
            return;

        foreach (var saveData in parent.SaveFileNodeDatas)
        {
            var bundle = _fileDataVmBundles
                .FirstOrDefault(b => b.FileData.DiskLabel == saveData.DiskLabel);
            if (bundle == null)
                continue;

            var parentFileNodeVm = TreeNavigationService.FindFileNodeVmByPath(
                bundle.FileNodeVm,
                saveData.FileNodePath,
                out _);
            var matchingChildFileNodeVm = parentFileNodeVm
                ?.Children
                .FirstOrDefault(c => c.Name == node.Name);
            if (matchingChildFileNodeVm == null)
                continue;

            var childFileNodePath = matchingChildFileNodeVm.FileNode.GetPath();
            var alreadyExists = node.SaveFileNodeDatas.Any(
                d => d.DiskLabel == bundle.FileData.DiskLabel
                     && d.FileNodePath == childFileNodePath);
            if (alreadyExists)
                continue;

            var newSaveData = new SaveFileNodeData
            {
                DiskLabel = bundle.FileData.DiskLabel,
                FileNodePath = childFileNodePath
            };
            node.SaveFileNodeDatas.Add(newSaveData);

            var newDeclareData = new DeclareRepoNodeData
            {
                RepoNodePath = node.GetPath()
            };
            matchingChildFileNodeVm.FileNode.DeclareRepoNodeDatas.Add(newDeclareData);
            matchingChildFileNodeVm.DeclareRepoNodeDatas.Add(
                (DeclareRepoNodeData)newDeclareData.Clone());

            var repoNodeVm = TreeNavigationService.FindRepoNodeVmByPath(
                _repoNodeVmRoot,
                node.GetPath(),
                out _);
            if (repoNodeVm != null && !repoNodeVm.SaveFileNodeDatas.Any(
                    d => d.DiskLabel == bundle.FileData.DiskLabel
                         && d.FileNodePath == childFileNodePath))
            {
                repoNodeVm.SaveFileNodeDatas.Add((SaveFileNodeData)newSaveData.Clone());
            }
        }
    }

    public void CheckAncestorsDeclarationStatus(RepoNode node)
    {
        var current = node;
        while (current != null)
        {
            var specificAffectedNodes = GetAffectedFileNodes(
                current,
                includeDescendants: false);
            UpdateAffectedFileNodesDeclaration(specificAffectedNodes);
            current = current.Parent as RepoNode;
        }
    }

    public List<(string DiskLabel, FileNode FileNode, RepoNode OriginalRepoNode)> GetAffectedFileNodes(
        RepoNode targetNode,
        bool includeDescendants = true)
    {
        var result = new List<(string, FileNode, RepoNode)>();
        var targetPathExact = targetNode.GetPath();
        var targetPathPrefix = targetPathExact + "/";

        void CollectFromTree(string diskLabel, FileNode fileNode)
        {
            foreach (var declareData in fileNode.DeclareRepoNodeDatas)
            {
                var isMatch = declareData.RepoNodePath == targetPathExact
                              || (includeDescendants
                                  && declareData.RepoNodePath.StartsWith(
                                      targetPathPrefix,
                                      StringComparison.Ordinal));

                if (!isMatch)
                    continue;

                var originalRepoNode = TreeNodeUtils.GetNodeByPathFromRoot(
                    _repoNodeRoot,
                    declareData.RepoNodePath) as RepoNode;
                if (originalRepoNode != null)
                    result.Add((diskLabel, fileNode, originalRepoNode));
            }

            foreach (var child in fileNode.Children.OfType<FileNode>())
                CollectFromTree(diskLabel, child);
        }

        foreach (var bundle in _fileDataVmBundles)
        {
            if (bundle.FileData?.FileNodeRoot != null)
                CollectFromTree(bundle.FileData.DiskLabel, bundle.FileData.FileNodeRoot);
        }

        return result;
    }

    public void UpdateAffectedFileNodesDeclaration(
        List<(string DiskLabel, FileNode FileNode, RepoNode OriginalRepoNode)> affectedNodes)
    {
        foreach (var (diskLabel, fileNode, repoNode) in affectedNodes)
        {
            var repoPath = repoNode.GetPath();
            var declareData = fileNode.DeclareRepoNodeDatas
                .FirstOrDefault(d => d.RepoNodePath == repoPath);
            if (declareData == null)
                continue;

            var currentRepoNodeInTree = TreeNodeUtils.GetNodeByPathFromRoot(
                _repoNodeRoot,
                repoPath) as RepoNode;
            if (currentRepoNodeInTree != null
                && TreeNodeUtils.CheckDeclarationStatus(currentRepoNodeInTree, fileNode))
            {
                continue;
            }

            fileNode.DeclareRepoNodeDatas.Remove(declareData);

            if (currentRepoNodeInTree != null)
                RemoveSaveFileNodeData(
                    currentRepoNodeInTree,
                    diskLabel,
                    fileNode.GetPath());

            RemoveDeclareDataFromFileNodeVm(diskLabel, fileNode.GetPath(), repoPath);
        }
    }

    public void UpdateRepoNodePathReferences(string oldPath, string newPath)
    {
        if (string.IsNullOrWhiteSpace(oldPath))
            return;

        foreach (var bundle in _fileDataVmBundles)
        {
            if (bundle.FileData?.FileNodeRoot != null)
                UpdateDeclareRepoNodePaths(bundle.FileData.FileNodeRoot, oldPath, newPath);

            if (bundle.FileNodeVm != null)
                UpdateDeclareRepoNodePaths(bundle.FileNodeVm, oldPath, newPath);
        }
    }

    private static FileNode BuildRefreshedFileNodeSubtree(
        FileNode? currentFileNode,
        FileNode scannedFileNode,
        TreeNodeBase? parent)
    {
        var refreshedFileNode = new FileNode
        {
            Name = scannedFileNode.Name,
            IsDirectory = scannedFileNode.IsDirectory,
            Parent = parent,
            DeclareRepoNodeDatas = currentFileNode?.DeclareRepoNodeDatas
                .Select(x => (DeclareRepoNodeData)x.Clone())
                .ToList() ?? new List<DeclareRepoNodeData>()
        };

        var currentChildrenByName = currentFileNode?.Children
            .OfType<FileNode>()
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var scannedChild in scannedFileNode.Children.OfType<FileNode>())
        {
            currentChildrenByName.TryGetValue(scannedChild.Name, out var matchingCurrentChild);
            refreshedFileNode.Children.Add(BuildRefreshedFileNodeSubtree(
                matchingCurrentChild,
                scannedChild,
                refreshedFileNode));
        }

        return refreshedFileNode;
    }

    private static bool IsPathInSubtree(
        string path,
        string subtreePath,
        string subtreePathPrefix)
    {
        return path == subtreePath
               || path.StartsWith(subtreePathPrefix, StringComparison.Ordinal);
    }

    private static FileNode? FindNodeInRefreshedSubtree(
        FileNode refreshedSubtreeRoot,
        string refreshedSubtreePath,
        string targetPath)
    {
        if (targetPath == refreshedSubtreePath)
            return refreshedSubtreeRoot;

        var subtreePathPrefix = refreshedSubtreePath + "/";
        if (!targetPath.StartsWith(subtreePathPrefix, StringComparison.Ordinal))
            return null;

        var relativeSegments = targetPath
            .Substring(subtreePathPrefix.Length)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = refreshedSubtreeRoot;
        foreach (var segment in relativeSegments)
        {
            current = current.Children
                .OfType<FileNode>()
                .FirstOrDefault(x => x.Name == segment);
            if (current == null)
                return null;
        }

        return current;
    }

    private static IEnumerable<RepoNode> EnumerateRepoNodes(RepoNode root)
    {
        yield return root;
        foreach (var child in root.Children.OfType<RepoNode>())
        {
            foreach (var descendant in EnumerateRepoNodes(child))
                yield return descendant;
        }
    }

    private void RemoveSaveFileNodeData(
        RepoNode repoNode,
        string diskLabel,
        string fileNodePath)
    {
        var saveData = repoNode.SaveFileNodeDatas
            .FirstOrDefault(d => d.DiskLabel == diskLabel
                                 && d.FileNodePath == fileNodePath);
        if (saveData != null)
            repoNode.SaveFileNodeDatas.Remove(saveData);

        var repoNodeVm = TreeNavigationService.FindRepoNodeVmByPath(
            _repoNodeVmRoot,
            repoNode.GetPath(),
            out _);
        var vmSaveData = repoNodeVm?.SaveFileNodeDatas
            .FirstOrDefault(d => d.DiskLabel == diskLabel
                                 && d.FileNodePath == fileNodePath);
        if (vmSaveData != null)
            repoNodeVm!.SaveFileNodeDatas.Remove(vmSaveData);
    }

    private void RemoveDeclareHolding(
        RepoNode repoNode,
        string diskLabel,
        string fileNodePath)
    {
        var repoPath = repoNode.GetPath();
        var fileNode = FindFileNode(diskLabel, fileNodePath);
        var declareData = fileNode?.DeclareRepoNodeDatas
            .FirstOrDefault(d => d.RepoNodePath == repoPath);
        if (declareData != null)
            fileNode!.DeclareRepoNodeDatas.Remove(declareData);

        RemoveSaveFileNodeData(repoNode, diskLabel, fileNodePath);
        RemoveDeclareDataFromFileNodeVm(diskLabel, fileNodePath, repoPath);
    }

    private void RemoveDeclareHolding(
        string repoPath,
        string diskLabel,
        string fileNodePath)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
            return;

        var repoNode = TreeNodeUtils.GetNodeByPathFromRoot(
            _repoNodeRoot,
            repoPath) as RepoNode;
        if (repoNode != null)
            RemoveSaveFileNodeData(repoNode, diskLabel, fileNodePath);

        var fileNode = FindFileNode(diskLabel, fileNodePath);
        var declareData = fileNode?.DeclareRepoNodeDatas
            .FirstOrDefault(d => d.RepoNodePath == repoPath);
        if (declareData != null)
            fileNode!.DeclareRepoNodeDatas.Remove(declareData);

        RemoveDeclareDataFromFileNodeVm(diskLabel, fileNodePath, repoPath);
    }

    private FileNode? FindFileNode(string diskLabel, string fileNodePath)
    {
        var bundle = _fileDataVmBundles
            .FirstOrDefault(b => b.FileData.DiskLabel == diskLabel);
        if (bundle == null)
            return null;

        return TreeNodeUtils.GetNodeByPathFromRoot(
            bundle.FileData.FileNodeRoot,
            fileNodePath) as FileNode;
    }

    private void RemoveDeclareDataFromFileNodeVm(
        string diskLabel,
        string fileNodePath,
        string repoPath)
    {
        foreach (var bundle in _fileDataVmBundles)
        {
            if (bundle.FileData.DiskLabel != diskLabel)
                continue;

            var vm = TreeNavigationService.FindFileNodeVmByPath(
                bundle.FileNodeVm,
                fileNodePath,
                out _);
            var vmDeclareData = vm?.DeclareRepoNodeDatas
                .FirstOrDefault(d => d.RepoNodePath == repoPath);
            if (vmDeclareData != null)
                vm!.DeclareRepoNodeDatas.Remove(vmDeclareData);
        }
    }

    private static void UpdateDeclareRepoNodePaths(
        FileNode node,
        string oldPath,
        string newPath)
    {
        foreach (var data in node.DeclareRepoNodeDatas)
        {
            if (data.RepoNodePath == null)
                continue;

            data.RepoNodePath = TreeNavigationService.ReplacePathPrefix(
                data.RepoNodePath,
                oldPath,
                newPath);
        }

        foreach (var child in node.Children.OfType<FileNode>())
            UpdateDeclareRepoNodePaths(child, oldPath, newPath);
    }

    private static void UpdateDeclareRepoNodePaths(
        FileNodeVM node,
        string oldPath,
        string newPath)
    {
        foreach (var data in node.DeclareRepoNodeDatas)
        {
            if (data.RepoNodePath == null)
                continue;

            data.RepoNodePath = TreeNavigationService.ReplacePathPrefix(
                data.RepoNodePath,
                oldPath,
                newPath);
        }

        foreach (var child in node.Children)
            UpdateDeclareRepoNodePaths(child, oldPath, newPath);
    }
}

public sealed record DeclareHoldingValidationFailure(
    string DiskLabel,
    string FileNodePath,
    string FailureReason,
    string RepoNodePath = "");
