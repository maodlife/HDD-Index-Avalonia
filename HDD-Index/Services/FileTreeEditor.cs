using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Declarations;
using HDD_Index.Application.FileTrees;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Services;

public sealed class FileTreeEditor : IFileTreeEditingService
{
    private readonly DeclarationSyncService _declarationSyncService;

    public FileTreeEditor(DeclarationSyncService declarationSyncService)
    {
        _declarationSyncService = declarationSyncService;
    }

    public bool CheckRepoNodeAndFileNodeIsSync(
        RepoNode? repoNode,
        FileNode? fileNode)
    {
        return _declarationSyncService.CheckRepoNodeAndFileNodeIsSync(
            repoNode,
            fileNode);
    }

    public FileNode BuildRefreshedFileNodeSubtree(
        FileNode currentFileNode,
        FileNode scannedFileNode)
    {
        return _declarationSyncService.BuildRefreshedFileNodeSubtree(
            currentFileNode,
            scannedFileNode);
    }

    public IReadOnlyList<DeclareHoldingValidationFailure>
        GetInvalidDeclareHoldingsAfterRefresh(
            string diskLabel,
            FileNode currentFileNode,
            FileNode refreshedFileNode)
    {
        return _declarationSyncService.GetInvalidDeclareHoldingsAfterRefresh(
            diskLabel,
            currentFileNode,
            refreshedFileNode);
    }

    public TreeChangeSet ApplyFileNodeRefresh(
        string diskLabel,
        FileNode currentFileNode,
        FileNode refreshedFileNode,
        IEnumerable<DeclareHoldingValidationFailure> failuresToRemove)
    {
        return _declarationSyncService.ApplyFileNodeRefresh(
            diskLabel,
            currentFileNode,
            refreshedFileNode,
            failuresToRemove);
    }

    /// <summary>
    /// 删除 FileNode 前后收集受影响节点：被删子树用于清理双向声明，
    /// 祖先链用于在结构变化后重新判断声明持有是否仍然成立。
    /// </summary>
    public TreeEditResult<FileNode> DeleteFileNode(
        FileNode fileNode,
        FileNode fileNodeRoot,
        string diskLabel)
    {
        if (fileNode == fileNodeRoot)
            return TreeEditResult<FileNode>.Failure("不能删除文件树根节点。");

        var parent = fileNode.Parent as FileNode;
        if (parent == null)
            return TreeEditResult<FileNode>.Failure("找不到文件节点的父目录。");

        var changes = new TreeChangeCollector();
        var deletedNodes = EnumerateFileNodes(fileNode).ToList();
        var ancestors = CollectAncestors(parent);

        changes.AddRange(
            _declarationSyncService.RemoveDeclareHoldingsFromFileNodes(
                diskLabel,
                deletedNodes));

        parent.Children.Remove(fileNode);
        changes.RemoveNode(parent, fileNode);

        changes.AddRange(
            _declarationSyncService.UpdateFileNodeDeclarations(
                diskLabel,
                ancestors));

        return TreeEditResult<FileNode>.Success(fileNode, changes.Build());
    }

    private static IEnumerable<FileNode> EnumerateFileNodes(FileNode root)
    {
        yield return root;
        foreach (var child in root.Children.OfType<FileNode>())
        {
            foreach (var descendant in EnumerateFileNodes(child))
                yield return descendant;
        }
    }

    private static List<FileNode> CollectAncestors(FileNode parent)
    {
        var ancestors = new List<FileNode>();
        var current = parent;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.Parent as FileNode;
        }

        return ancestors;
    }
}
