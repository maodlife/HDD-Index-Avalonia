using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class RepoTreeEditorTests
{
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
}
