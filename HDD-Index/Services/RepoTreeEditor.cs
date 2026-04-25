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
