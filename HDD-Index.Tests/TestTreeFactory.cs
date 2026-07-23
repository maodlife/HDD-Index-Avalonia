using HDD_Index.Models;

namespace HDD_Index.Tests;

// 测试辅助工厂：用很少的代码构造内存里的 Repo 树、File 树和 FileData。
// 这样测试不需要访问真实磁盘，也不需要打开 Avalonia 窗口，可以专注验证服务层逻辑。
internal static class TestTreeFactory
{
    // 构造一个 RepoNode，并把传入的 children 挂到它下面。
    // 这里会同时设置 child.Parent，保证 GetPath() 等依赖父节点的逻辑能正常工作。
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

    public static RepoNode RepoFile(string name)
    {
        return new RepoNode
        {
            Name = name,
            IsDirectory = false
        };
    }

    // 构造一个 FileNode，并把传入的 children 挂到它下面。
    // FileNode 模拟真实磁盘文件树中的目录节点。
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

    public static FileNode DiskFile(string name)
    {
        return new FileNode
        {
            Name = name,
            IsDirectory = false
        };
    }

    public static FileData Bundle(string diskLabel, FileNode root)
    {
        return new FileData
        {
            DiskLabel = diskLabel,
            FileNodeRoot = root
        };
    }
}
