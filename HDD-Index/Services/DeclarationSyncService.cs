using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Declarations;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Services;

public class DeclarationSyncService : IDeclarationHoldingService
{
    private readonly RepoNode _repoNodeRoot;
    private readonly IList<FileData> _fileDatas;

    public DeclarationSyncService(
        RepoNode repoNodeRoot,
        IList<FileData> fileDatas)
    {
        _repoNodeRoot = repoNodeRoot;
        _fileDatas = fileDatas;
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

    public TreeEditResult<bool> TryDeclareHolding(
        RepoNode repoNode,
        FileNode fileNode,
        string diskLabel,
        DeclareHoldingStrategyType strategyType,
        bool saveStrategyToRepoNode)
    {
        var strategy = DeclareHoldingStrategyFactory.Create(strategyType);
        if (!strategy.CheckDeclareHolding(repoNode, fileNode, out var failureReason))
            return TreeEditResult<bool>.Failure(failureReason);

        var changes = new TreeChangeCollector();

        if (saveStrategyToRepoNode)
        {
            repoNode.DeclareHoldingStrategyType = strategyType;
            changes.Refresh(repoNode, TreeNodePresentation.Strategy);
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
            changes.Refresh(repoNode, TreeNodePresentation.Relationships);
        }

        var declareDataExists = fileNode.DeclareRepoNodeDatas.Any(
            d => d.RepoNodePath == repoNodePath);
        if (!declareDataExists)
        {
            fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
            {
                RepoNodePath = repoNodePath
            });
            changes.Refresh(fileNode, TreeNodePresentation.Relationships);
        }

        return TreeEditResult<bool>.Success(true, changes.Build());
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

    public TreeChangeSet ApplyDeclareHoldingStrategy(
        RepoNode repoNode,
        DeclareHoldingStrategyType? strategyType,
        IEnumerable<DeclareHoldingValidationFailure> failuresToRemove)
    {
        var changes = new TreeChangeCollector();
        repoNode.DeclareHoldingStrategyType = strategyType;
        changes.Refresh(repoNode, TreeNodePresentation.Strategy);

        foreach (var failure in failuresToRemove.ToList())
        {
            RemoveDeclareHolding(
                repoNode,
                failure.DiskLabel,
                failure.FileNodePath,
                changes);
        }
        return changes.Build();
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

    public TreeChangeSet ApplyFileNodeRefresh(
        string diskLabel,
        FileNode currentFileNode,
        FileNode refreshedFileNode,
        IEnumerable<DeclareHoldingValidationFailure> failuresToRemove)
    {
        var changes = new TreeChangeCollector();
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

        changes.ReplaceSubtree(currentFileNode);

        foreach (var failure in failuresToRemove)
            RemoveDeclareHolding(
                failure.RepoNodePath,
                diskLabel,
                failure.FileNodePath,
                changes);

        return changes.Build();
    }

    public TreeChangeSet AbandonDeclareHoldings(
        FileNode fileNode,
        string diskLabel,
        IEnumerable<string> repoNodePaths)
    {
        var changes = new TreeChangeCollector();
        if (string.IsNullOrWhiteSpace(diskLabel))
            return changes.Build();

        var fileNodePath = fileNode.GetPath();
        foreach (var repoNodePath in repoNodePaths
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct())
        {
            var declareData = fileNode.DeclareRepoNodeDatas
                .FirstOrDefault(d => d.RepoNodePath == repoNodePath);
            if (declareData != null)
            {
                fileNode.DeclareRepoNodeDatas.Remove(declareData);
                changes.Refresh(fileNode, TreeNodePresentation.Relationships);
            }

            var repoNode = TreeNodeUtils.GetNodeByPathFromRoot(
                _repoNodeRoot,
                repoNodePath) as RepoNode;
            if (repoNode != null)
                RemoveSaveFileNodeData(repoNode, diskLabel, fileNodePath, changes);
        }

        return changes.Build();
    }

    /// <summary>
    /// 删除 FileNode 子树前，先移除这些节点上记录的声明持有关系，
    /// 以同步清理 RepoNode 里的 SaveFileNodeData。
    /// </summary>
    public TreeChangeSet RemoveDeclareHoldingsFromFileNodes(
        string diskLabel,
        IEnumerable<FileNode> fileNodes)
    {
        var changes = new TreeChangeCollector();
        if (string.IsNullOrWhiteSpace(diskLabel))
            return changes.Build();

        foreach (var fileNode in fileNodes.Distinct().ToList())
        {
            var fileNodePath = fileNode.GetPath();
            var repoNodePaths = fileNode.DeclareRepoNodeDatas
                .Select(x => x.RepoNodePath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            foreach (var repoNodePath in repoNodePaths)
                RemoveDeclareHolding(repoNodePath, diskLabel, fileNodePath, changes);
        }

        return changes.Build();
    }

    /// <summary>
    /// FileNode 子树变更后重新检查仍存在节点上的声明持有关系，
    /// 不再满足策略或指向缺失 RepoNode 的关系会被双向移除。
    /// </summary>
    public TreeChangeSet UpdateFileNodeDeclarations(
        string diskLabel,
        IEnumerable<FileNode> fileNodes)
    {
        var changes = new TreeChangeCollector();
        if (string.IsNullOrWhiteSpace(diskLabel))
            return changes.Build();

        foreach (var fileNode in fileNodes.Distinct().ToList())
        {
            var fileNodePath = fileNode.GetPath();
            foreach (var declareData in fileNode.DeclareRepoNodeDatas.ToList())
            {
                var repoNodePath = declareData.RepoNodePath;
                if (string.IsNullOrWhiteSpace(repoNodePath))
                {
                    fileNode.DeclareRepoNodeDatas.Remove(declareData);
                    changes.Refresh(fileNode, TreeNodePresentation.Relationships);
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

                RemoveDeclareHolding(repoNodePath, diskLabel, fileNodePath, changes);
            }
        }

        return changes.Build();
    }

    public TreeChangeSet TryEstablishSaveFileNodeDatasForNode(RepoNode node)
    {
        var changes = new TreeChangeCollector();
        var parent = node.Parent as RepoNode;
        if (parent == null || !parent.SaveFileNodeDatas.Any())
            return changes.Build();

        foreach (var saveData in parent.SaveFileNodeDatas)
        {
            var fileData = _fileDatas
                .FirstOrDefault(x => x.DiskLabel == saveData.DiskLabel);
            if (fileData == null)
                continue;

            var parentFileNode = TreeNodeUtils.GetNodeByPathFromRoot(
                fileData.FileNodeRoot,
                saveData.FileNodePath) as FileNode;
            var matchingChildFileNode = parentFileNode
                ?.Children
                .OfType<FileNode>()
                .FirstOrDefault(c => c.Name == node.Name);
            if (matchingChildFileNode == null)
                continue;

            var childFileNodePath = matchingChildFileNode.GetPath();
            var alreadyExists = node.SaveFileNodeDatas.Any(
                d => d.DiskLabel == fileData.DiskLabel
                     && d.FileNodePath == childFileNodePath);
            if (alreadyExists)
                continue;

            var newSaveData = new SaveFileNodeData
            {
                DiskLabel = fileData.DiskLabel,
                FileNodePath = childFileNodePath
            };
            node.SaveFileNodeDatas.Add(newSaveData);
            changes.Refresh(node, TreeNodePresentation.Relationships);

            var newDeclareData = new DeclareRepoNodeData
            {
                RepoNodePath = node.GetPath()
            };
            matchingChildFileNode.DeclareRepoNodeDatas.Add(newDeclareData);
            changes.Refresh(matchingChildFileNode, TreeNodePresentation.Relationships);
        }

        return changes.Build();
    }

    public TreeChangeSet CheckAncestorsDeclarationStatus(RepoNode node)
    {
        var changes = new TreeChangeCollector();
        var current = node;
        while (current != null)
        {
            var specificAffectedNodes = GetAffectedFileNodes(
                current,
                includeDescendants: false);
            changes.AddRange(UpdateAffectedFileNodesDeclaration(specificAffectedNodes));
            current = current.Parent as RepoNode;
        }
        return changes.Build();
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

        foreach (var fileData in _fileDatas)
        {
            if (fileData.FileNodeRoot != null)
                CollectFromTree(fileData.DiskLabel, fileData.FileNodeRoot);
        }

        return result;
    }

    public TreeChangeSet UpdateAffectedFileNodesDeclaration(
        List<(string DiskLabel, FileNode FileNode, RepoNode OriginalRepoNode)> affectedNodes)
    {
        var changes = new TreeChangeCollector();
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
            changes.Refresh(fileNode, TreeNodePresentation.Relationships);

            if (currentRepoNodeInTree != null)
                RemoveSaveFileNodeData(
                    currentRepoNodeInTree,
                    diskLabel,
                    fileNode.GetPath(),
                    changes);
        }
        return changes.Build();
    }

    public TreeChangeSet UpdateRepoNodePathReferences(string oldPath, string newPath)
    {
        var changes = new TreeChangeCollector();
        if (string.IsNullOrWhiteSpace(oldPath))
            return changes.Build();

        foreach (var fileData in _fileDatas)
        {
            if (fileData.FileNodeRoot != null)
                UpdateDeclareRepoNodePaths(
                    fileData.FileNodeRoot,
                    oldPath,
                    newPath,
                    changes);
        }
        return changes.Build();
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
        string fileNodePath,
        TreeChangeCollector changes)
    {
        var saveData = repoNode.SaveFileNodeDatas
            .FirstOrDefault(d => d.DiskLabel == diskLabel
                                 && d.FileNodePath == fileNodePath);
        if (saveData != null)
        {
            repoNode.SaveFileNodeDatas.Remove(saveData);
            changes.Refresh(repoNode, TreeNodePresentation.Relationships);
        }
    }

    private void RemoveDeclareHolding(
        RepoNode repoNode,
        string diskLabel,
        string fileNodePath,
        TreeChangeCollector changes)
    {
        var repoPath = repoNode.GetPath();
        var fileNode = FindFileNode(diskLabel, fileNodePath);
        var declareData = fileNode?.DeclareRepoNodeDatas
            .FirstOrDefault(d => d.RepoNodePath == repoPath);
        if (declareData != null)
        {
            fileNode!.DeclareRepoNodeDatas.Remove(declareData);
            changes.Refresh(fileNode, TreeNodePresentation.Relationships);
        }

        RemoveSaveFileNodeData(repoNode, diskLabel, fileNodePath, changes);
    }

    private void RemoveDeclareHolding(
        string repoPath,
        string diskLabel,
        string fileNodePath,
        TreeChangeCollector changes)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
            return;

        var repoNode = TreeNodeUtils.GetNodeByPathFromRoot(
            _repoNodeRoot,
            repoPath) as RepoNode;
        if (repoNode != null)
            RemoveSaveFileNodeData(repoNode, diskLabel, fileNodePath, changes);

        var fileNode = FindFileNode(diskLabel, fileNodePath);
        var declareData = fileNode?.DeclareRepoNodeDatas
            .FirstOrDefault(d => d.RepoNodePath == repoPath);
        if (declareData != null)
        {
            fileNode!.DeclareRepoNodeDatas.Remove(declareData);
            changes.Refresh(fileNode, TreeNodePresentation.Relationships);
        }
    }

    private FileNode? FindFileNode(string diskLabel, string fileNodePath)
    {
        var fileData = _fileDatas
            .FirstOrDefault(x => x.DiskLabel == diskLabel);
        if (fileData == null)
            return null;

        return TreeNodeUtils.GetNodeByPathFromRoot(
            fileData.FileNodeRoot,
            fileNodePath) as FileNode;
    }

    private static void UpdateDeclareRepoNodePaths(
        FileNode node,
        string oldPath,
        string newPath,
        TreeChangeCollector changes)
    {
        var changed = false;
        foreach (var data in node.DeclareRepoNodeDatas)
        {
            if (data.RepoNodePath == null)
                continue;

            var replaced = TreeNodeUtils.ReplacePathPrefix(
                data.RepoNodePath,
                oldPath,
                newPath);
            if (replaced == data.RepoNodePath)
                continue;

            data.RepoNodePath = replaced;
            changed = true;
        }

        if (changed)
            changes.Refresh(node, TreeNodePresentation.Relationships);

        foreach (var child in node.Children.OfType<FileNode>())
            UpdateDeclareRepoNodePaths(child, oldPath, newPath, changes);
    }
}
