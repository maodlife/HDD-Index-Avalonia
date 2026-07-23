using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.ViewModels;

/// <summary>
/// Maintains the session-local projection from pure model objects to tree view models.
/// Model object identity is stable for the duration of a loaded application session.
/// </summary>
public sealed class TreeProjection
{
    private readonly Dictionary<RepoNode, RepoNodeVM> _repoNodeVms = new();
    private readonly Dictionary<FileNode, FileNodeVM> _fileNodeVms = new();

    public RepoNodeVM CreateRepoTree(RepoNode root)
        => RegisterRepoSubtree(root);

    public FileNodeVM CreateFileTree(FileNode root)
        => RegisterFileSubtree(root);

    public RepoNodeVM GetRepoNodeVm(RepoNode node)
        => _repoNodeVms[node];

    public FileNodeVM GetFileNodeVm(FileNode node)
        => _fileNodeVms[node];

    public bool TryGetRepoNodeVm(RepoNode node, out RepoNodeVM? viewModel)
        => _repoNodeVms.TryGetValue(node, out viewModel);

    public bool TryGetFileNodeVm(FileNode node, out FileNodeVM? viewModel)
        => _fileNodeVms.TryGetValue(node, out viewModel);

    public void Apply(TreeChangeSet changeSet)
    {
        foreach (var change in changeSet.Changes)
        {
            switch (change)
            {
                case TreeNodeAdded added:
                    ApplyNodeAdded(added);
                    break;
                case TreeNodeRemoved removed:
                    ApplyNodeRemoved(removed);
                    break;
                case TreeNodePresentationChanged changed:
                    ApplyPresentationChanged(changed);
                    break;
                case FileNodeSubtreeReplaced replaced:
                    ApplySubtreeReplaced(replaced.Node);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change));
            }
        }
    }

    private void ApplyNodeAdded(TreeNodeAdded change)
    {
        switch (change.Parent, change.Node)
        {
            case (RepoNode parent, RepoNode child):
            {
                var childVm = RegisterRepoSubtree(child);
                _repoNodeVms[parent].Children.Insert(change.Index, childVm);
                break;
            }
            case (FileNode parent, FileNode child):
            {
                var childVm = RegisterFileSubtree(child);
                _fileNodeVms[parent].Children.Insert(change.Index, childVm);
                break;
            }
            default:
                throw new InvalidOperationException("父子节点类型不一致。");
        }
    }

    private void ApplyNodeRemoved(TreeNodeRemoved change)
    {
        switch (change.Parent, change.Node)
        {
            case (RepoNode parent, RepoNode child):
                if (_repoNodeVms.TryGetValue(parent, out var parentRepoVm)
                    && _repoNodeVms.TryGetValue(child, out var childRepoVm))
                {
                    parentRepoVm.Children.Remove(childRepoVm);
                    UnregisterRepoSubtree(child);
                }
                break;
            case (FileNode parent, FileNode child):
                if (_fileNodeVms.TryGetValue(parent, out var parentFileVm)
                    && _fileNodeVms.TryGetValue(child, out var childFileVm))
                {
                    parentFileVm.Children.Remove(childFileVm);
                    UnregisterFileSubtree(child);
                }
                break;
        }
    }

    private void ApplyPresentationChanged(TreeNodePresentationChanged change)
    {
        if (change.Node is RepoNode repoNode
            && _repoNodeVms.TryGetValue(repoNode, out var repoVm))
        {
            repoVm.Refresh(change.Presentation);
        }
        else if (change.Node is FileNode fileNode
                 && _fileNodeVms.TryGetValue(fileNode, out var fileVm))
        {
            fileVm.Refresh(change.Presentation);
        }
    }

    private void ApplySubtreeReplaced(FileNode node)
    {
        if (!_fileNodeVms.TryGetValue(node, out var nodeVm))
            return;

        foreach (var oldChild in nodeVm.Children.Select(x => x.FileNode).ToList())
            UnregisterFileSubtree(oldChild);

        nodeVm.Children.Clear();
        foreach (var child in node.Children.OfType<FileNode>())
            nodeVm.Children.Add(RegisterFileSubtree(child));

        nodeVm.Refresh(TreeNodePresentation.All);
    }

    private RepoNodeVM RegisterRepoSubtree(RepoNode node)
    {
        if (_repoNodeVms.TryGetValue(node, out var existing))
            return existing;

        var vm = new RepoNodeVM(node);
        _repoNodeVms.Add(node, vm);
        foreach (var child in node.Children.OfType<RepoNode>())
            vm.Children.Add(RegisterRepoSubtree(child));
        return vm;
    }

    private FileNodeVM RegisterFileSubtree(FileNode node)
    {
        if (_fileNodeVms.TryGetValue(node, out var existing))
            return existing;

        var vm = new FileNodeVM(node);
        _fileNodeVms.Add(node, vm);
        foreach (var child in node.Children.OfType<FileNode>())
            vm.Children.Add(RegisterFileSubtree(child));
        return vm;
    }

    private void UnregisterRepoSubtree(RepoNode node)
    {
        foreach (var child in node.Children.OfType<RepoNode>())
            UnregisterRepoSubtree(child);
        _repoNodeVms.Remove(node);
    }

    private void UnregisterFileSubtree(FileNode node)
    {
        foreach (var child in node.Children.OfType<FileNode>())
            UnregisterFileSubtree(child);
        _fileNodeVms.Remove(node);
    }
}
