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
