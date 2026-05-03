using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

// 这个文件测试 FileTreeEditor 对文件树索引的删除行为，
// 尤其是删除后如何同步维护 RepoNode 与 FileNode 的声明持有关系。
public class FileTreeEditorTests
{
    [Fact]
    public void DeleteFileNode_RemovesBidirectionalDeclarationData()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.RepoFile("movie.mkv"));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.DiskFile("movie.mkv"));
        var fileNode = (FileNode)fileRoot.Children[0];
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNode.GetPath()
        });
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var editor = CreateEditor(repoRoot, repoRootVm, bundle);

        var deleted = editor.DeleteFileNode(
            (FileNodeVM)bundle.FileNodeVm.Children[0],
            fileRoot,
            bundle.FileNodeVm,
            "DiskA");

        Assert.True(deleted);
        Assert.Empty(fileRoot.Children);
        Assert.Empty(bundle.FileNodeVm.Children);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(((RepoNodeVM)repoRootVm.Children[0]).SaveFileNodeDatas);
    }

    [Fact]
    public void DeleteFileNode_RemovesAncestorDeclarationWhenSubtreeNoLongerMatches()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.RepoFile("movie.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                TestTreeFactory.DiskFile("movie.mkv")));
        var moviesFileNode = (FileNode)fileRoot.Children[0];
        var movieFileNode = (FileNode)moviesFileNode.Children[0];
        moviesFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = moviesFileNode.GetPath()
        });
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var editor = CreateEditor(repoRoot, repoRootVm, bundle);
        var moviesFileNodeVm = (FileNodeVM)bundle.FileNodeVm.Children[0];

        var deleted = editor.DeleteFileNode(
            (FileNodeVM)moviesFileNodeVm.Children[0],
            fileRoot,
            bundle.FileNodeVm,
            "DiskA");

        Assert.True(deleted);
        Assert.Empty(moviesFileNode.Children);
        Assert.Empty(moviesFileNodeVm.Children);
        Assert.Empty(moviesFileNode.DeclareRepoNodeDatas);
        Assert.Empty(moviesFileNodeVm.DeclareRepoNodeDatas);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(((RepoNodeVM)repoRootVm.Children[0]).SaveFileNodeDatas);
        Assert.Equal("Disk/Movies/movie.mkv", movieFileNode.GetPath());
    }

    [Fact]
    public void DeleteFileNode_ReturnsFalseForRoot()
    {
        var repoRoot = TestTreeFactory.Repo("Root");
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.DiskFile("movie.mkv"));
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var editor = CreateEditor(repoRoot, repoRootVm, bundle);

        var deleted = editor.DeleteFileNode(
            bundle.FileNodeVm,
            fileRoot,
            bundle.FileNodeVm,
            "DiskA");

        Assert.False(deleted);
        Assert.Single(fileRoot.Children);
        Assert.Single(bundle.FileNodeVm.Children);
    }

    private static FileTreeEditor CreateEditor(
        RepoNode repoRoot,
        RepoNodeVM repoRootVm,
        FileDataVMBundle bundle)
    {
        var syncService = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });
        return new FileTreeEditor(syncService);
    }
}
