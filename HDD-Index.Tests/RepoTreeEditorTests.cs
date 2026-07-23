using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class RepoTreeEditorTests
{
    [Fact]
    public void CreateChildFolder_AddsUniqueFolderAndEstablishesMatchingSaveData()
    {
        var repoRoot = TestTreeFactory.Repo("Root");
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("新建文件夹"));
        repoRoot.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = "Disk"
        });
        var editor = CreateEditor(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = editor.CreateChildFolder(repoRoot);

        Assert.True(result.Succeeded);
        var created = Assert.IsType<RepoNode>(result.Value);
        Assert.Equal("新建文件夹", created.Name);
        Assert.Single(created.SaveFileNodeDatas);
        Assert.Equal("Root/新建文件夹",
            ((FileNode)fileRoot.Children[0]).DeclareRepoNodeDatas.Single().RepoNodePath);
        Assert.False(result.Changes.IsEmpty);
    }

    [Fact]
    public void CreateChildFolder_UsesUniqueName()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("新建文件夹"),
            TestTreeFactory.Repo("新建文件夹 (1)"));
        var editor = CreateEditor(repoRoot);

        var result = editor.CreateChildFolder(repoRoot);

        Assert.Equal("新建文件夹 (2)", result.Value!.Name);
    }

    [Fact]
    public void RenameRepoNode_ReturnsFailureWhenSiblingNameConflicts()
    {
        var first = TestTreeFactory.Repo("First");
        var second = TestTreeFactory.Repo("Second");
        var root = TestTreeFactory.Repo("Root", first, second);
        var editor = CreateEditor(root);

        var result = editor.RenameRepoNode(first, "Second");

        Assert.False(result.Succeeded);
        Assert.Equal("First", first.Name);
    }

    [Fact]
    public void RenameRepoNode_RemovesDeclarationWhenNamesNoLongerMatch()
    {
        var repoNode = TestTreeFactory.Repo("Movies");
        var root = TestTreeFactory.Repo("Root", repoNode);
        var fileNode = TestTreeFactory.File("Movies");
        var fileRoot = TestTreeFactory.File("Disk", fileNode);
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Movies"
        });
        var editor = CreateEditor(root, TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = editor.RenameRepoNode(repoNode, "Films");

        Assert.True(result.Succeeded);
        Assert.Equal("Films", repoNode.Name);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
    }

    [Fact]
    public void CopyFileNodeSubtreeToRepoDirectory_CopiesShapeWithoutDeclarationData()
    {
        var root = TestTreeFactory.Repo("Root");
        var source = TestTreeFactory.File(
            "Movies",
            TestTreeFactory.File("Series", TestTreeFactory.DiskFile("episode.mkv")));
        source.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Other"
        });
        var editor = CreateEditor(root);

        var result = editor.CopyFileNodeSubtreeToRepoDirectory(root, source);

        Assert.True(result.Succeeded);
        var copied = result.Value!;
        Assert.Equal("Movies", copied.Name);
        Assert.Empty(copied.SaveFileNodeDatas);
        Assert.Equal("episode.mkv", copied.Children[0].Children[0].Name);
    }

    [Fact]
    public void CopyFileNodeSubtreeToRepoDirectory_ReturnsFailureOnConflict()
    {
        var root = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var editor = CreateEditor(root);

        var result = editor.CopyFileNodeSubtreeToRepoDirectory(
            root,
            TestTreeFactory.File("Movies"));

        Assert.False(result.Succeeded);
        Assert.Single(root.Children);
    }

    [Fact]
    public void FindDescendantRepoNodesByName_ExcludesSelectedNodeAndIgnoresCase()
    {
        var nested = TestTreeFactory.Repo("movie");
        var selected = TestTreeFactory.Repo("Movie", nested);
        var root = TestTreeFactory.Repo("Root", selected);
        var editor = CreateEditor(root);

        var matches = editor.FindDescendantRepoNodesByName(selected, "MOVIE");

        Assert.Single(matches);
        Assert.Same(nested, matches[0]);
    }

    [Fact]
    public void DeleteRepoNodes_RemovesOutermostMatchesAndDeclarations()
    {
        var child = TestTreeFactory.Repo("Target");
        var parent = TestTreeFactory.Repo("Target", child);
        var root = TestTreeFactory.Repo("Root", parent);
        var fileNode = TestTreeFactory.File("Target");
        var fileRoot = TestTreeFactory.File("Disk", fileNode);
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = parent.GetPath()
        });
        parent.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNode.GetPath()
        });
        var editor = CreateEditor(root, TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = editor.DeleteRepoNodes(new[] { parent, child }, root);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value);
        Assert.Empty(root.Children);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
    }

    [Fact]
    public void DeleteRepoNode_ReturnsFailureForRoot()
    {
        var root = TestTreeFactory.Repo("Root");
        var editor = CreateEditor(root);

        var result = editor.DeleteRepoNode(root, root);

        Assert.False(result.Succeeded);
    }

    private static RepoTreeEditor CreateEditor(
        RepoNode repoRoot,
        params FileData[] fileDatas)
        => new(new DeclarationSyncService(repoRoot, fileDatas.ToList()));
}
