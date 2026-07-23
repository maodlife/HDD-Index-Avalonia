using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class FileTreeEditorTests
{
    [Fact]
    public void DeleteFileNode_RemovesBidirectionalDeclarationData()
    {
        var repoNode = TestTreeFactory.Repo("Movies");
        var repoRoot = TestTreeFactory.Repo("Root", repoNode);
        var fileNode = TestTreeFactory.File("Movies");
        var fileRoot = TestTreeFactory.File("Disk", fileNode);
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNode.GetPath()
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        var editor = CreateEditor(
            repoRoot,
            TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = editor.DeleteFileNode(fileNode, fileRoot, "DiskA");

        Assert.True(result.Succeeded);
        Assert.Empty(fileRoot.Children);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.False(result.Changes.IsEmpty);
    }

    [Fact]
    public void DeleteFileNode_RemovesAncestorDeclarationWhenSubtreeNoLongerMatches()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("movie.mkv"));
        var repoRoot = TestTreeFactory.Repo("Root", repoNode);
        var movieFile = TestTreeFactory.DiskFile("movie.mkv");
        var fileNode = TestTreeFactory.File("Movies", movieFile);
        var fileRoot = TestTreeFactory.File("Disk", fileNode);
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNode.GetPath()
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        var editor = CreateEditor(
            repoRoot,
            TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = editor.DeleteFileNode(movieFile, fileRoot, "DiskA");

        Assert.True(result.Succeeded);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(repoNode.SaveFileNodeDatas);
    }

    [Fact]
    public void DeleteFileNode_ReturnsFailureForRoot()
    {
        var repoRoot = TestTreeFactory.Repo("Root");
        var fileRoot = TestTreeFactory.File("Disk");
        var editor = CreateEditor(
            repoRoot,
            TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = editor.DeleteFileNode(fileRoot, fileRoot, "DiskA");

        Assert.False(result.Succeeded);
    }

    private static FileTreeEditor CreateEditor(
        RepoNode repoRoot,
        params FileData[] fileDatas)
        => new(new DeclarationSyncService(repoRoot, fileDatas.ToList()));
}
