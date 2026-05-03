using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

// 这个文件测试 RepoTreeEditor 对“仓库树”的编辑行为。
// 重点不是界面按钮本身，而是按钮背后会调用的业务逻辑：
// 新建节点时如何命名、如何加入模型和 ViewModel、如何同步到对应的文件树声明。
public class RepoTreeEditorTests
{
    // 场景：
    // 1. 仓库根节点 Root 下面已经有一个“新建文件夹”。
    // 2. 文件树 Disk 下面有一个同名候选节点“新建文件夹 (1)”。
    // 3. Root 已经保存到 DiskA 的 Disk 根节点，因此新建子文件夹时可以尝试匹配文件树中的同名节点。
    //
    // 期望：
    // 新建出的仓库节点名为“新建文件夹 (1)”，被加入 Root.Children；
    // 同时它会记录一条 DiskA 的 SaveFileNodeData，文件树对应节点也会声明自己关联到这个仓库节点。
    [Fact]
    public void CreateChildFolder_AddsUniqueFolderAndEstablishesMatchingSaveData()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("新建文件夹"));
        var parentRepoVm = RepoNodeVM.Create(repoRoot);
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File("新建文件夹 (1)"));
        var parentFileNode = fileRoot;
        repoRoot.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = parentFileNode.GetPath()
        });
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var syncService = new DeclarationSyncService(
            repoRoot,
            parentRepoVm,
            new List<FileDataVMBundle> { bundle });
        var editor = new RepoTreeEditor(syncService);

        var createdVm = editor.CreateChildFolder(parentRepoVm);

        Assert.Equal("新建文件夹 (1)", createdVm.Name);
        Assert.Contains(createdVm.RepoNode, repoRoot.Children);
        Assert.Single(createdVm.SaveFileNodeDatas);
        Assert.Equal("DiskA", createdVm.SaveFileNodeDatas[0].DiskLabel);
        Assert.Single(((FileNode)fileRoot.Children[0]).DeclareRepoNodeDatas);
        Assert.Equal(createdVm.RepoNode.GetPath(), ((FileNode)fileRoot.Children[0]).DeclareRepoNodeDatas[0].RepoNodePath);
    }

    // 场景：Root 下面已经有 Movies 和 Books，尝试把 Movies 重命名成 Books。
    // 期望：重命名失败，返回 false，并且原节点名字仍然保持 Movies。
    [Fact]
    public void RenameRepoNode_ReturnsFalseWhenSiblingNameConflicts()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies"),
            TestTreeFactory.Repo("Books"));
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var syncService = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle>());
        var editor = new RepoTreeEditor(syncService);

        var renamed = editor.RenameRepoNode(repoRootVm.Children[0], "Books");

        Assert.False(renamed);
        Assert.Equal("Movies", repoRootVm.Children[0].Name);
    }

    [Fact]
    public void CopyFileNodeSubtreeToRepoDirectory_CopiesShapeWithoutDeclarationData()
    {
        var repoRoot = TestTreeFactory.Repo("Root");
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var source = TestTreeFactory.File(
            "Movies",
            TestTreeFactory.DiskFile("movie.mkv"),
            TestTreeFactory.File("Extras", TestTreeFactory.DiskFile("poster.jpg")));
        source.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Movies"
        });
        var editor = CreateEditor(repoRoot, repoRootVm);

        var copiedVm = editor.CopyFileNodeSubtreeToRepoDirectory(repoRootVm, source);

        Assert.NotNull(copiedVm);
        Assert.Equal("Movies", copiedVm.Name);
        Assert.False(copiedVm.SaveFileNodeDatas.Any());
        Assert.Equal(new[] { "movie.mkv", "Extras" },
            copiedVm.Children.Select(x => x.Name));
        Assert.False(((RepoNode)repoRoot.Children[0]).SaveFileNodeDatas.Any());
        Assert.Null(((RepoNode)repoRoot.Children[0]).DeclareHoldingStrategyType);
    }

    [Fact]
    public void CopyFileNodeSubtreeToRepoDirectory_SkipsRootWhenTargetHasSameName()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var source = TestTreeFactory.File("Movies", TestTreeFactory.DiskFile("movie.mkv"));
        var editor = CreateEditor(repoRoot, repoRootVm);

        var copiedVm = editor.CopyFileNodeSubtreeToRepoDirectory(repoRootVm, source);

        Assert.Null(copiedVm);
        Assert.Single(repoRoot.Children);
        Assert.Single(repoRootVm.Children);
    }

    [Fact]
    public void CopyFileNodeSubtreeToRepoDirectory_SkipsConflictingSiblingsInsideSource()
    {
        var repoRoot = TestTreeFactory.Repo("Root");
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var source = TestTreeFactory.File(
            "Movies",
            TestTreeFactory.DiskFile("movie.mkv"),
            TestTreeFactory.DiskFile("movie.mkv"),
            TestTreeFactory.DiskFile("poster.jpg"));
        var editor = CreateEditor(repoRoot, repoRootVm);

        var copiedVm = editor.CopyFileNodeSubtreeToRepoDirectory(repoRootVm, source);

        Assert.NotNull(copiedVm);
        Assert.Equal(new[] { "movie.mkv", "poster.jpg" },
            copiedVm.Children.Select(x => x.Name));
        Assert.Equal(new[] { "movie.mkv", "poster.jpg" },
            ((RepoNode)repoRoot.Children[0]).Children
            .OfType<RepoNode>()
            .Select(x => x.Name));
    }

    [Fact]
    public void FindDescendantRepoNodesByName_DoesNotIncludeSelectedNode()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.Repo("Movies")));
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var selectedVm = repoRootVm.Children[0];
        var editor = CreateEditor(repoRoot, repoRootVm);

        var matches = editor.FindDescendantRepoNodesByName(selectedVm, "Movies");

        Assert.Single(matches);
        Assert.Equal("Root/Movies/Movies", matches[0].RepoNode.GetPath());
    }

    [Fact]
    public void FindDescendantRepoNodesByName_MatchesFilesAndDirectoriesIgnoringCase()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("MEDIA"),
            TestTreeFactory.RepoFile("MOVIE.MKV"));
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var editor = CreateEditor(repoRoot, repoRootVm);

        var directoryMatches = editor.FindDescendantRepoNodesByName(repoRootVm, "media");
        var fileMatches = editor.FindDescendantRepoNodesByName(repoRootVm, "movie.mkv");

        Assert.Single(directoryMatches);
        Assert.True(directoryMatches[0].RepoNode.IsDirectory);
        Assert.Single(fileMatches);
        Assert.False(fileMatches[0].RepoNode.IsDirectory);
    }

    [Fact]
    public void DeleteRepoNodes_RemovesMatchedDirectorySubtreeAndDeclarations()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.Repo(
                    "Anime",
                    TestTreeFactory.RepoFile("episode.mkv"))));
        var animeRepoNode = (RepoNode)((RepoNode)repoRoot.Children[0]).Children[0];
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                TestTreeFactory.File(
                    "Anime",
                    TestTreeFactory.DiskFile("episode.mkv"))));
        var animeFileNode = (FileNode)((FileNode)fileRoot.Children[0]).Children[0];
        animeFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = animeRepoNode.GetPath()
        });
        animeRepoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = animeFileNode.GetPath()
        });
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var editor = new RepoTreeEditor(new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle }));
        var animeRepoVm = repoRootVm.Children[0].Children[0];

        var deleted = editor.DeleteRepoNodes(
            new[] { animeRepoVm, animeRepoVm.Children[0] },
            repoRoot,
            repoRootVm);

        Assert.True(deleted);
        Assert.Empty(((RepoNode)repoRoot.Children[0]).Children);
        Assert.Empty(repoRootVm.Children[0].Children);
        Assert.Empty(animeFileNode.DeclareRepoNodeDatas);
        Assert.Empty(((FileNodeVM)((FileNodeVM)bundle.FileNodeVm.Children[0]).Children[0]).DeclareRepoNodeDatas);
    }

    [Fact]
    public void DeleteRepoNode_ReturnsFalseForRoot()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var editor = CreateEditor(repoRoot, repoRootVm);

        var deleted = editor.DeleteRepoNode(repoRootVm, repoRoot, repoRootVm);

        Assert.False(deleted);
        Assert.Single(repoRoot.Children);
    }

    private static RepoTreeEditor CreateEditor(RepoNode repoRoot, RepoNodeVM repoRootVm)
    {
        var syncService = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle>());
        return new RepoTreeEditor(syncService);
    }
}
