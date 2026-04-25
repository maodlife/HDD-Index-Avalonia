using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

internal static class TestTreeFactory
{
    public static RepoNode Repo(string name, params RepoNode[] children)
    {
        var node = new RepoNode
        {
            Name = name,
            IsDirectory = true
        };

        foreach (var child in children)
        {
            child.Parent = node;
            node.Children.Add(child);
        }

        return node;
    }

    public static FileNode File(string name, params FileNode[] children)
    {
        var node = new FileNode
        {
            Name = name,
            IsDirectory = true
        };

        foreach (var child in children)
        {
            child.Parent = node;
            node.Children.Add(child);
        }

        return node;
    }

    public static FileDataVMBundle Bundle(string diskLabel, FileNode root)
    {
        return new FileDataVMBundle
        {
            FileData = new FileData
            {
                DiskLabel = diskLabel,
                FileNodeRoot = root
            },
            FileNodeVm = FileNodeVM.Create(root)
        };
    }
}
