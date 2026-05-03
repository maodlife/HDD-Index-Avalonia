using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Services;

public class RepoTreeEditor
{
    private readonly DeclarationSyncService _declarationSyncService;

    public RepoTreeEditor(DeclarationSyncService declarationSyncService)
    {
        _declarationSyncService = declarationSyncService;
    }

    public RepoNodeVM CreateChildFolder(RepoNodeVM parentVm)
    {
        var folderName = CreateUniqueChildFolderName(parentVm);
        var newRepoNode = new RepoNode
        {
            Name = folderName,
            IsDirectory = true,
            Parent = parentVm.RepoNode
        };
        parentVm.RepoNode.Children.Add(newRepoNode);

        var newRepoNodeVm = RepoNodeVM.Create(newRepoNode);
        parentVm.Children.Add(newRepoNodeVm);

        _declarationSyncService.TryEstablishSaveFileNodeDatasForNode(newRepoNode);
        SyncSaveFileNodeDatas(newRepoNodeVm);
        _declarationSyncService.CheckAncestorsDeclarationStatus(parentVm.RepoNode);

        return newRepoNodeVm;
    }

    public bool RenameRepoNode(RepoNodeVM repoNodeVm, string newName)
    {
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName.Contains('/'))
            return false;

        if (string.Equals(newName, repoNodeVm.Name, StringComparison.Ordinal))
            return false;

        var parent = repoNodeVm.RepoNode.Parent as RepoNode;
        if (parent != null && HasSiblingNameConflict(parent, repoNodeVm.RepoNode, newName))
            return false;

        var oldPath = repoNodeVm.RepoNode.GetPath();
        repoNodeVm.RepoNode.Name = newName;
        repoNodeVm.Name = newName;
        var newPath = repoNodeVm.RepoNode.GetPath();

        _declarationSyncService.UpdateRepoNodePathReferences(oldPath, newPath);
        var affectedFileNodes = _declarationSyncService.GetAffectedFileNodes(
            repoNodeVm.RepoNode,
            includeDescendants: false);
        _declarationSyncService.UpdateAffectedFileNodesDeclaration(affectedFileNodes);
        _declarationSyncService.TryEstablishSaveFileNodeDatasForNode(repoNodeVm.RepoNode);
        SyncSaveFileNodeDatas(repoNodeVm);
        _declarationSyncService.CheckAncestorsDeclarationStatus(repoNodeVm.RepoNode);

        return true;
    }

    public bool DeleteRepoNode(RepoNodeVM repoNodeVm, RepoNode repoNodeRoot, RepoNodeVM repoNodeVmRoot)
    {
        if (repoNodeVm.RepoNode == repoNodeRoot)
            return false;

        var parent = repoNodeVm.RepoNode.Parent as RepoNode;
        if (parent == null)
            return false;

        var affectedFileNodes = _declarationSyncService.GetAffectedFileNodes(repoNodeVm.RepoNode);
        var ancestors = CollectAncestors(parent);

        parent.Children.Remove(repoNodeVm.RepoNode);

        var parentVm = TreeNavigationService.FindRepoNodeVmByPath(
            repoNodeVmRoot,
            parent.GetPath(),
            out _);
        parentVm?.Children.Remove(repoNodeVm);

        _declarationSyncService.UpdateAffectedFileNodesDeclaration(affectedFileNodes);
        foreach (var ancestor in ancestors)
            _declarationSyncService.CheckAncestorsDeclarationStatus(ancestor);

        return true;
    }

    public IReadOnlyList<RepoNodeVM> FindDescendantRepoNodesByName(
        RepoNodeVM repoNodeVm,
        string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            return Array.Empty<RepoNodeVM>();

        var result = new List<RepoNodeVM>();
        foreach (var child in repoNodeVm.Children)
            CollectMatchingDescendants(child, nodeName, result);

        return result;
    }

    /// <summary>
    /// 批量删除搜索命中的 RepoNode。命中目录及其子节点同时命中时，只删除最外层命中节点，
    /// 避免重复处理已经随父目录删除的子树。
    /// </summary>
    public bool DeleteRepoNodes(
        IEnumerable<RepoNodeVM> repoNodeVms,
        RepoNode repoNodeRoot,
        RepoNodeVM repoNodeVmRoot)
    {
        var deleteTargets = ExcludeNestedRepoNodes(repoNodeVms, repoNodeRoot);
        var deletedAny = false;
        foreach (var deleteTarget in deleteTargets)
        {
            deletedAny |= DeleteRepoNode(
                deleteTarget,
                repoNodeRoot,
                repoNodeVmRoot);
        }

        return deletedAny;
    }

    public RepoNodeVM? CopyFileNodeSubtreeToRepoDirectory(
        RepoNodeVM targetParentVm,
        FileNode sourceFileNode)
    {
        if (!targetParentVm.RepoNode.IsDirectory
            || HasChildNameConflict(targetParentVm.RepoNode, sourceFileNode.Name))
        {
            return null;
        }

        var copiedNode = CopyFileNodeToRepoNode(sourceFileNode, targetParentVm.RepoNode);
        targetParentVm.RepoNode.Children.Add(copiedNode);

        var copiedVm = RepoNodeVM.Create(copiedNode);
        targetParentVm.Children.Add(copiedVm);
        return copiedVm;
    }

    private static string CreateUniqueChildFolderName(RepoNodeVM parentVm)
    {
        const string baseName = "新建文件夹";
        var folderName = baseName;
        var counter = 1;

        while (parentVm.Children.Any(c => c.Name == folderName))
        {
            folderName = $"{baseName} ({counter})";
            counter++;
        }

        return folderName;
    }

    private static bool HasSiblingNameConflict(
        RepoNode parent,
        RepoNode current,
        string newName)
    {
        return parent.Children
            .OfType<RepoNode>()
            .Any(x => !ReferenceEquals(x, current)
                      && string.Equals(x.Name, newName, StringComparison.Ordinal));
    }

    private static bool HasChildNameConflict(RepoNode parent, string childName)
    {
        return parent.Children
            .OfType<RepoNode>()
            .Any(x => string.Equals(x.Name, childName, StringComparison.Ordinal));
    }

    private static RepoNode CopyFileNodeToRepoNode(FileNode source, RepoNode? parent)
    {
        var copiedNode = new RepoNode
        {
            Name = source.Name,
            IsDirectory = source.IsDirectory,
            Parent = parent
        };

        foreach (var sourceChild in source.Children.OfType<FileNode>())
        {
            if (HasChildNameConflict(copiedNode, sourceChild.Name))
                continue;

            copiedNode.Children.Add(CopyFileNodeToRepoNode(sourceChild, copiedNode));
        }

        return copiedNode;
    }

    private static void CollectMatchingDescendants(
        RepoNodeVM repoNodeVm,
        string nodeName,
        ICollection<RepoNodeVM> result)
    {
        if (string.Equals(repoNodeVm.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            result.Add(repoNodeVm);

        foreach (var child in repoNodeVm.Children)
            CollectMatchingDescendants(child, nodeName, result);
    }

    private static List<RepoNodeVM> ExcludeNestedRepoNodes(
        IEnumerable<RepoNodeVM> repoNodeVms,
        RepoNode repoNodeRoot)
    {
        var candidates = repoNodeVms
            .Where(x => x.RepoNode != repoNodeRoot)
            .Distinct()
            .ToList();
        var candidateNodes = candidates
            .Select(x => x.RepoNode)
            .ToHashSet();

        return candidates
            .Where(x => !HasAncestorInSet(x.RepoNode, candidateNodes))
            .ToList();
    }

    private static bool HasAncestorInSet(
        RepoNode repoNode,
        ISet<RepoNode> repoNodes)
    {
        var current = repoNode.Parent as RepoNode;
        while (current != null)
        {
            if (repoNodes.Contains(current))
                return true;

            current = current.Parent as RepoNode;
        }

        return false;
    }

    private static List<RepoNode> CollectAncestors(RepoNode parent)
    {
        var ancestors = new List<RepoNode>();
        var current = parent;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.Parent as RepoNode;
        }

        return ancestors;
    }

    private static void SyncSaveFileNodeDatas(RepoNodeVM repoNodeVm)
    {
        repoNodeVm.SaveFileNodeDatas.Clear();
        foreach (var data in repoNodeVm.RepoNode.SaveFileNodeDatas)
            repoNodeVm.SaveFileNodeDatas.Add((SaveFileNodeData)data.Clone());
    }
}
