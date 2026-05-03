using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

// 这个文件测试 TreeNavigationService 的“路径处理”和“按路径找树节点”能力。
// 所有数据都在内存里手工构造，不依赖真实磁盘文件，也不启动 Avalonia 界面。
public class TreeNavigationServiceTests
{
    // 场景：路径本身刚好等于旧前缀。
    // 期望：整个路径被替换为新前缀。
    [Fact]
    public void ReplacePathPrefix_ReplacesExactPath()
    {
        var result = TreeNavigationService.ReplacePathPrefix(
            "Root/Movies",
            "Root/Movies",
            "Root/Films");

        Assert.Equal("Root/Films", result);
    }

    // 场景：路径是旧前缀下面的子路径。
    // 期望：只替换前缀部分，后面的子路径 /Anime 保持不变。
    [Fact]
    public void ReplacePathPrefix_ReplacesDescendantPath()
    {
        var result = TreeNavigationService.ReplacePathPrefix(
            "Root/Movies/Anime",
            "Root/Movies",
            "Root/Films");

        Assert.Equal("Root/Films/Anime", result);
    }

    // 场景：路径开头文字相似，但不是同一个路径段，比如 MoviesArchive 不是 Movies 的子节点。
    // 期望：不能误替换，原路径保持不变。
    [Fact]
    public void ReplacePathPrefix_DoesNotReplacePartialSegment()
    {
        var result = TreeNavigationService.ReplacePathPrefix(
            "Root/MoviesArchive/Anime",
            "Root/Movies",
            "Root/Films");

        Assert.Equal("Root/MoviesArchive/Anime", result);
    }

    // 场景：构造一棵 Repo 树 Root -> Books -> SciFi，并把它转换成 RepoNodeVM。
    // 期望：按路径 Root/Books/SciFi 能找到 SciFi 节点，同时返回它在树中的索引路径 [0, 1, 0]。
    [Fact]
    public void FindRepoNodeVmByPath_ReturnsNodeAndIndexPath()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies"),
            TestTreeFactory.Repo(
                "Books",
                TestTreeFactory.Repo("SciFi")));
        var rootVm = RepoNodeVM.Create(repoRoot);

        var result = TreeNavigationService.FindRepoNodeVmByPath(
            rootVm,
            "Root/Books/SciFi",
            out var indexPath);

        Assert.NotNull(result);
        Assert.Equal("SciFi", result.Name);
        Assert.NotNull(indexPath);
        Assert.Equal(3, indexPath.Value.Count);
        Assert.Equal(0, indexPath.Value[0]);
        Assert.Equal(1, indexPath.Value[1]);
        Assert.Equal(0, indexPath.Value[2]);
    }

    [Fact]
    public void FindRepoExpandPathsToSavedNodes_ReturnsPathsWithinCurrentSubtree()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                CreateSavedRepoFile("movie.mkv")),
            CreateSavedRepoFile("book.epub"));
        var rootVm = RepoNodeVM.Create(repoRoot);
        var moviesVm = rootVm.Children[0];

        var paths = TreeNavigationService.FindRepoExpandPathsToSavedNodes(moviesVm);

        Assert.Single(paths);
        AssertIndexPath(paths[0], 0);
    }

    [Fact]
    public void FindRepoExpandPathsToSavedNodes_StopsAtSavedNode()
    {
        var savedDirectory = TestTreeFactory.Repo(
            "Movies",
            CreateSavedRepoFile("movie.mkv"));
        savedDirectory.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "Disk",
            FileNodePath = "Disk/Movies"
        });
        var repoRoot = TestTreeFactory.Repo("Root", savedDirectory);
        var rootVm = RepoNodeVM.Create(repoRoot);

        var paths = TreeNavigationService.FindRepoExpandPathsToSavedNodes(rootVm);

        Assert.Single(paths);
        AssertIndexPath(paths[0], 0);
    }

    [Fact]
    public void FindRepoExpandPathsToSavedNodes_ExpandsSharedAncestorsOnce()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                CreateSavedRepoFile("movie.mkv"),
                CreateSavedRepoFile("clip.mp4")));
        var rootVm = RepoNodeVM.Create(repoRoot);

        var paths = TreeNavigationService.FindRepoExpandPathsToSavedNodes(rootVm);

        Assert.Equal(2, paths.Count);
        AssertIndexPath(paths[0], 0);
        AssertIndexPath(paths[1], 0, 0);
    }

    [Fact]
    public void FindFileExpandPathsToDeclaredNodes_ReturnsPathsToDeclaredNodes()
    {
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                CreateDeclaredDiskFile("movie.mkv")));
        var rootVm = FileNodeVM.Create(fileRoot);

        var paths = TreeNavigationService.FindFileExpandPathsToDeclaredNodes(rootVm);

        Assert.Equal(2, paths.Count);
        AssertIndexPath(paths[0], 0);
        AssertIndexPath(paths[1], 0, 0);
    }

    private static RepoNode CreateSavedRepoFile(string name)
    {
        var node = TestTreeFactory.RepoFile(name);
        node.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "Disk",
            FileNodePath = $"Disk/{name}"
        });
        return node;
    }

    private static FileNode CreateDeclaredDiskFile(string name)
    {
        var node = TestTreeFactory.DiskFile(name);
        node.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = $"Root/{name}"
        });
        return node;
    }

    private static void AssertIndexPath(Avalonia.Controls.IndexPath path, params int[] indexes)
    {
        Assert.Equal(indexes.Length, path.Count);
        for (var i = 0; i < indexes.Length; i++)
            Assert.Equal(indexes[i], path[i]);
    }
}
