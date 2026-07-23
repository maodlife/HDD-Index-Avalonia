using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;
using HDD_Index.Services;

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
        var service = CreateService(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot));

        Assert.True(service.CheckRepoNodeAndFileNodeIsSync(repoNode, fileNode));
    }

    [Fact]
    public void TryDeclareHolding_AddsBidirectionalModelDataAndChanges()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("movie.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File("Movies", TestTreeFactory.DiskFile("movie.mkv")));
        var fileNode = (FileNode)fileRoot.Children[0];
        var service = CreateService(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = service.TryDeclareHolding(
            repoNode,
            fileNode,
            "DiskA",
            DeclareHoldingStrategyType.Default,
            saveStrategyToRepoNode: true);

        Assert.True(result.Succeeded);
        Assert.Equal(DeclareHoldingStrategyType.Default, repoNode.DeclareHoldingStrategyType);
        Assert.Contains(repoNode.SaveFileNodeDatas,
            x => x.DiskLabel == "DiskA" && x.FileNodePath == "Disk/Movies");
        Assert.Contains(fileNode.DeclareRepoNodeDatas,
            x => x.RepoNodePath == "Root/Movies");
        Assert.False(result.Changes.IsEmpty);
    }

    [Fact]
    public void TryDeclareHolding_DoesNotAddDuplicates()
    {
        var (service, repoNode, fileNode) = CreateMatchingTrees();

        service.TryDeclareHolding(
            repoNode, fileNode, "DiskA", DeclareHoldingStrategyType.Default, false);
        service.TryDeclareHolding(
            repoNode, fileNode, "DiskA", DeclareHoldingStrategyType.Default, false);

        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
    }

    [Fact]
    public void TryDeclareHolding_ReturnsFailureWithoutMutationWhenValidationFails()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("required.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        var service = CreateService(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = service.TryDeclareHolding(
            repoNode, fileNode, "DiskA", DeclareHoldingStrategyType.Default, true);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.FailureReason);
        Assert.Null(repoNode.DeclareHoldingStrategyType);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
    }

    [Fact]
    public void ApplyDeclareHoldingStrategy_RemovesInvalidBidirectionalData()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("required.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        AddDeclaration(repoNode, fileNode, "DiskA");
        var service = CreateService(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot));
        var failures = service.GetInvalidSaveFileNodeDatasForStrategy(
            repoNode, DeclareHoldingStrategyType.Default);

        var changes = service.ApplyDeclareHoldingStrategy(
            repoNode, DeclareHoldingStrategyType.Default, failures);

        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.False(changes.IsEmpty);
    }

    [Fact]
    public void AbandonDeclareHoldings_RemovesOnlySelectedRelation()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies"),
            TestTreeFactory.Repo("Archive"));
        var movies = (RepoNode)repoRoot.Children[0];
        var archive = (RepoNode)repoRoot.Children[1];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        AddDeclaration(movies, fileNode, "DiskA");
        AddDeclaration(archive, fileNode, "DiskA");
        var service = CreateService(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot));

        service.AbandonDeclareHoldings(
            fileNode, "DiskA", new[] { movies.GetPath() });

        Assert.Empty(movies.SaveFileNodeDatas);
        Assert.Single(archive.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
        Assert.Equal(archive.GetPath(), fileNode.DeclareRepoNodeDatas[0].RepoNodePath);
    }

    [Fact]
    public void UpdateRepoNodePathReferences_UpdatesDescendantReferences()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Old/Child"
        });
        var service = CreateService(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot));

        var changes = service.UpdateRepoNodePathReferences("Root/Old", "Root/New");

        Assert.Equal("Root/New/Child", fileNode.DeclareRepoNodeDatas[0].RepoNodePath);
        Assert.False(changes.IsEmpty);
    }

    [Fact]
    public void ApplyFileNodeRefresh_PreservesValidDeclaration()
    {
        var (service, repoNode, fileNode) = CreateMatchingTrees();
        service.TryDeclareHolding(
            repoNode, fileNode, "DiskA", DeclareHoldingStrategyType.Default, false);
        var scanned = TestTreeFactory.File(
            "Movies", TestTreeFactory.DiskFile("movie.mkv"), TestTreeFactory.DiskFile("new.txt"));
        var refreshed = service.BuildRefreshedFileNodeSubtree(fileNode, scanned);
        var failures = service.GetInvalidDeclareHoldingsAfterRefresh(
            "DiskA", fileNode, refreshed);

        var changes = service.ApplyFileNodeRefresh(
            "DiskA", fileNode, refreshed, failures);

        Assert.Empty(failures);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
        Assert.Contains(fileNode.Children, x => x.Name == "new.txt");
        Assert.Contains(changes.Changes, x => x is FileNodeSubtreeReplaced);
    }

    [Fact]
    public void ApplyFileNodeRefresh_RemovesInvalidBidirectionalDeclaration()
    {
        var (service, repoNode, fileNode) = CreateMatchingTrees();
        service.TryDeclareHolding(
            repoNode, fileNode, "DiskA", DeclareHoldingStrategyType.Default, false);
        var refreshed = service.BuildRefreshedFileNodeSubtree(
            fileNode,
            TestTreeFactory.File("Movies"));
        var failures = service.GetInvalidDeclareHoldingsAfterRefresh(
            "DiskA", fileNode, refreshed);

        service.ApplyFileNodeRefresh("DiskA", fileNode, refreshed, failures);

        Assert.NotEmpty(failures);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(repoNode.SaveFileNodeDatas);
    }

    private static (DeclarationSyncService Service, RepoNode RepoNode, FileNode FileNode)
        CreateMatchingTrees()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("movie.mkv")));
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File("Movies", TestTreeFactory.DiskFile("movie.mkv")));
        return (
            CreateService(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot)),
            (RepoNode)repoRoot.Children[0],
            (FileNode)fileRoot.Children[0]);
    }

    private static DeclarationSyncService CreateService(
        RepoNode repoRoot,
        params FileData[] fileDatas)
        => new(repoRoot, fileDatas.ToList());

    private static void AddDeclaration(
        RepoNode repoNode,
        FileNode fileNode,
        string diskLabel)
    {
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = diskLabel,
            FileNodePath = fileNode.GetPath()
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
    }
}
