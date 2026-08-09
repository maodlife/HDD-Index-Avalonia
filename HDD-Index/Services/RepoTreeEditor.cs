using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Repositories;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Services;

public class RepoTreeEditor : IRepositoryEditingService
{
    private readonly DeclarationSyncService _declarationSyncService;

    public RepoTreeEditor(DeclarationSyncService declarationSyncService)
    {
        _declarationSyncService = declarationSyncService;
    }

    public TreeEditResult<RepoNode> CreateChildFolder(RepoNode parent)
    {
        var changes = new TreeChangeCollector();
        var folderName = CreateUniqueChildFolderName(parent);
        var newRepoNode = new RepoNode
        {
            Name = folderName,
            IsDirectory = true,
            Parent = parent
        };
        parent.Children.Add(newRepoNode);
        changes.AddNode(parent, newRepoNode, parent.Children.Count - 1);
        changes.AddRange(
            _declarationSyncService.TryEstablishSaveFileNodeDatasForNode(newRepoNode));
        changes.AddRange(
            _declarationSyncService.CheckAncestorsDeclarationStatus(parent));

        return TreeEditResult<RepoNode>.Success(newRepoNode, changes.Build());
    }

    public TreeEditResult<RepoNode> RenameRepoNode(RepoNode repoNode, string newName)
    {
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName.Contains('/'))
            return TreeEditResult<RepoNode>.Failure();

        if (string.Equals(newName, repoNode.Name, StringComparison.Ordinal))
            return TreeEditResult<RepoNode>.Failure();

        var parent = repoNode.Parent as RepoNode;
        if (parent != null && HasSiblingNameConflict(parent, repoNode, newName))
            return TreeEditResult<RepoNode>.Failure();

        var changes = new TreeChangeCollector();
        var oldPath = repoNode.GetPath();
        repoNode.Name = newName;
        changes.Refresh(repoNode, TreeNodePresentation.Name);
        var newPath = repoNode.GetPath();

        changes.AddRange(
            _declarationSyncService.UpdateRepoNodePathReferences(oldPath, newPath));
        var affectedFileNodes = _declarationSyncService.GetAffectedFileNodes(
            repoNode,
            includeDescendants: false);
        changes.AddRange(
            _declarationSyncService.UpdateAffectedFileNodesDeclaration(affectedFileNodes));
        changes.AddRange(
            _declarationSyncService.TryEstablishSaveFileNodeDatasForNode(repoNode));
        changes.AddRange(
            _declarationSyncService.CheckAncestorsDeclarationStatus(repoNode));

        return TreeEditResult<RepoNode>.Success(repoNode, changes.Build());
    }

    public TreeEditResult<RepoNode> DeleteRepoNode(
        RepoNode repoNode,
        RepoNode repoNodeRoot)
    {
        if (repoNode == repoNodeRoot)
            return TreeEditResult<RepoNode>.Failure();

        var parent = repoNode.Parent as RepoNode;
        if (parent == null)
            return TreeEditResult<RepoNode>.Failure();

        var changes = new TreeChangeCollector();
        var affectedFileNodes = _declarationSyncService.GetAffectedFileNodes(repoNode);
        var ancestors = CollectAncestors(parent);

        parent.Children.Remove(repoNode);
        changes.RemoveNode(parent, repoNode);

        changes.AddRange(
            _declarationSyncService.UpdateAffectedFileNodesDeclaration(affectedFileNodes));
        foreach (var ancestor in ancestors)
            changes.AddRange(
                _declarationSyncService.CheckAncestorsDeclarationStatus(ancestor));

        return TreeEditResult<RepoNode>.Success(repoNode, changes.Build());
    }

    public IReadOnlyList<RepoNode> FindDescendantRepoNodesByName(
        RepoNode repoNode,
        string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            return Array.Empty<RepoNode>();

        var result = new List<RepoNode>();
        foreach (var child in repoNode.Children.OfType<RepoNode>())
            CollectMatchingDescendants(child, nodeName, result);

        return result;
    }

    /// <summary>
    /// 批量删除搜索命中的 RepoNode。命中目录及其子节点同时命中时，只删除最外层命中节点，
    /// 避免重复处理已经随父目录删除的子树。
    /// </summary>
    public TreeEditResult<int> DeleteRepoNodes(
        IEnumerable<RepoNode> repoNodes,
        RepoNode repoNodeRoot)
    {
        var changes = new TreeChangeCollector();
        var deleteTargets = ExcludeNestedRepoNodes(repoNodes, repoNodeRoot);
        var deletedCount = 0;
        foreach (var deleteTarget in deleteTargets)
        {
            var result = DeleteRepoNode(
                deleteTarget,
                repoNodeRoot);
            if (!result.Succeeded)
                continue;
            deletedCount++;
            changes.AddRange(result.Changes);
        }

        return deletedCount > 0
            ? TreeEditResult<int>.Success(deletedCount, changes.Build())
            : TreeEditResult<int>.Failure();
    }

    public TreeEditResult<RepoNode> CopyFileNodeSubtreeToRepoDirectory(
        RepoNode targetParent,
        FileNode sourceFileNode)
    {
        if (!targetParent.IsDirectory
            || HasChildNameConflict(targetParent, sourceFileNode.Name))
        {
            return TreeEditResult<RepoNode>.Failure();
        }

        var copiedNode = CopyFileNodeToRepoNode(sourceFileNode, targetParent);
        targetParent.Children.Add(copiedNode);
        var changes = new TreeChangeCollector();
        changes.AddNode(targetParent, copiedNode, targetParent.Children.Count - 1);
        return TreeEditResult<RepoNode>.Success(copiedNode, changes.Build());
    }

    private static string CreateUniqueChildFolderName(RepoNode parent)
    {
        const string baseName = "新建文件夹";
        var folderName = baseName;
        var counter = 1;

        while (parent.Children.Any(c => c.Name == folderName))
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
        RepoNode repoNode,
        string nodeName,
        ICollection<RepoNode> result)
    {
        if (string.Equals(repoNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            result.Add(repoNode);

        foreach (var child in repoNode.Children.OfType<RepoNode>())
            CollectMatchingDescendants(child, nodeName, result);
    }

    private static List<RepoNode> ExcludeNestedRepoNodes(
        IEnumerable<RepoNode> repoNodes,
        RepoNode repoNodeRoot)
    {
        var candidates = repoNodes
            .Where(x => x != repoNodeRoot)
            .Distinct()
            .ToList();
        var candidateNodes = candidates.ToHashSet();

        return candidates
            .Where(x => !HasAncestorInSet(x, candidateNodes))
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

}
