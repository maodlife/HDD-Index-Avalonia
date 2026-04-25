using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class DeclarationSyncServiceTests
{
    [Fact]
    public void CheckRepoNodeAndFileNodeIsSync_ReturnsTrueWhenFileDeclaresSelectedRepo()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });

        var service = new DeclarationSyncService(
            repoRoot,
            RepoNodeVM.Create(repoRoot),
            new List<FileDataVMBundle> { TestTreeFactory.Bundle("DiskA", fileRoot) });

        Assert.True(service.CheckRepoNodeAndFileNodeIsSync(repoNode, fileNode));
    }

    [Fact]
    public void UpdateAffectedFileNodesDeclaration_RemovesDeclarationWhenRepoSubtreeNoLongerMatches()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.Repo("Anime")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
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
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });

        var affectedNodes = service.GetAffectedFileNodes(repoNode, includeDescendants: false);
        service.UpdateAffectedFileNodesDeclaration(affectedNodes);

        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(((FileNodeVM)bundle.FileNodeVm.Children[0]).DeclareRepoNodeDatas);
    }

    [Fact]
    public void UpdateAffectedFileNodesDeclaration_RemovesOnlyAffectedDiskWhenFilePathsMatch()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.Repo("Anime")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var invalidFileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var invalidFileNode = (FileNode)invalidFileRoot.Children[0];
        var validFileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                TestTreeFactory.File("Anime")));
        var validFileNode = (FileNode)validFileRoot.Children[0];
        invalidFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        validFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskB",
            FileNodePath = validFileNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = invalidFileNode.GetPath()
        });
        var invalidBundle = TestTreeFactory.Bundle("DiskA", invalidFileRoot);
        var validBundle = TestTreeFactory.Bundle("DiskB", validFileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { invalidBundle, validBundle });

        var affectedNodes = service.GetAffectedFileNodes(repoNode, includeDescendants: false);
        service.UpdateAffectedFileNodesDeclaration(affectedNodes);

        Assert.Empty(invalidFileNode.DeclareRepoNodeDatas);
        Assert.Single(validFileNode.DeclareRepoNodeDatas);
        Assert.DoesNotContain(repoNode.SaveFileNodeDatas, x => x.DiskLabel == "DiskA");
        Assert.Contains(repoNode.SaveFileNodeDatas, x => x.DiskLabel == "DiskB");
        Assert.Empty(invalidBundle.FileNodeVm.Children[0].DeclareRepoNodeDatas);
        Assert.Single(validBundle.FileNodeVm.Children[0].DeclareRepoNodeDatas);
    }
}
