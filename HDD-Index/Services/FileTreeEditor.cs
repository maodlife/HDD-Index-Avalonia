using System.Collections.Generic;
using System.Linq;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Services;

public class FileTreeEditor
{
    private readonly DeclarationSyncService _declarationSyncService;

    public FileTreeEditor(DeclarationSyncService declarationSyncService)
    {
        _declarationSyncService = declarationSyncService;
    }

    /// <summary>
    /// 删除 FileNode 前后收集受影响节点：被删子树用于清理双向声明，
    /// 祖先链用于在结构变化后重新判断声明持有是否仍然成立。
    /// </summary>
    public bool DeleteFileNode(
        FileNodeVM fileNodeVm,
        FileNode fileNodeRoot,
        FileNodeVM fileNodeVmRoot,
        string diskLabel)
    {
        if (fileNodeVm.FileNode == fileNodeRoot)
            return false;

        var parent = fileNodeVm.FileNode.Parent as FileNode;
        if (parent == null)
            return false;

        var deletedNodes = EnumerateFileNodes(fileNodeVm.FileNode).ToList();
        var ancestors = CollectAncestors(parent);

        _declarationSyncService.RemoveDeclareHoldingsFromFileNodes(
            diskLabel,
            deletedNodes);

        parent.Children.Remove(fileNodeVm.FileNode);

        var parentVm = TreeNavigationService.FindFileNodeVmByPath(
            fileNodeVmRoot,
            parent.GetPath(),
            out _);
        parentVm?.Children.Remove(fileNodeVm);

        _declarationSyncService.UpdateFileNodeDeclarations(
            diskLabel,
            ancestors);

        return true;
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
