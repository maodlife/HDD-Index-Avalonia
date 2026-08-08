using HDD_Index.Application.Declarations;
using HDD_Index.Application.Persistence;
using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class DeclarationUseCasesTests
{
    [Fact]
    public void DeclareHolding_WithInitialStrategyReturnsChangesAndPersistenceTargets()
    {
        var (useCases, repoNode, fileNode) = CreateMatchingTrees();

        var result = useCases.DeclareHolding(
            repoNode,
            fileNode,
            "DiskA",
            DeclareHoldingStrategyType.Default);

        Assert.True(result.Succeeded);
        Assert.Equal(
            DeclareHoldingStrategyType.Default,
            repoNode.DeclareHoldingStrategyType);
        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
        Assert.False(result.Changes.IsEmpty);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
            },
            result.PersistenceTargets);
    }

    [Fact]
    public void DeclareHolding_WithoutAvailableStrategyFailsWithoutMutation()
    {
        var (useCases, repoNode, fileNode) = CreateMatchingTrees();

        var result = useCases.DeclareHolding(
            repoNode,
            fileNode,
            "DiskA",
            selectedInitialStrategyType: null);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.FailureReason);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(result.PersistenceTargets);
    }

    [Fact]
    public void DeclareHolding_UsesStoredStrategyInsteadOfInitialSelection()
    {
        var (useCases, repoNode, fileNode) = CreateMatchingTrees();
        repoNode.DeclareHoldingStrategyType = DeclareHoldingStrategyType.Default;

        var result = useCases.DeclareHolding(
            repoNode,
            fileNode,
            "DiskA",
            DeclareHoldingStrategyType.BDRip);

        Assert.True(result.Succeeded);
        Assert.Equal(
            DeclareHoldingStrategyType.Default,
            repoNode.DeclareHoldingStrategyType);
        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
    }

    [Fact]
    public void DeclareHolding_WhenValidationFailsReturnsNoPersistenceTargets()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("required.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        var useCases = CreateUseCases(
            repoRoot,
            TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = useCases.DeclareHolding(
            repoNode,
            fileNode,
            "DiskA",
            DeclareHoldingStrategyType.Default);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.FailureReason);
        Assert.Null(repoNode.DeclareHoldingStrategyType);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(result.PersistenceTargets);
    }

    [Fact]
    public void GetDeclaredRepoNodePathsFiltersEmptyAndDuplicatePaths()
    {
        var (useCases, _, fileNode) = CreateMatchingTrees();
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Movies",
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = string.Empty,
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Movies",
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Archive",
        });

        var paths = useCases.GetDeclaredRepoNodePaths(fileNode);

        Assert.Equal(new[] { "Root/Movies", "Root/Archive" }, paths);
    }

    [Fact]
    public void AbandonDeclareHoldingsRemovesSelectionAndReturnsPersistenceTargets()
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
        var useCases = CreateUseCases(
            repoRoot,
            TestTreeFactory.Bundle("DiskA", fileRoot));

        var result = useCases.AbandonDeclareHoldings(
            fileNode,
            "DiskA",
            [movies.GetPath()]);

        Assert.True(result.Succeeded);
        Assert.Empty(movies.SaveFileNodeDatas);
        Assert.Single(archive.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
        Assert.False(result.Changes.IsEmpty);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
            },
            result.PersistenceTargets);
    }

    [Fact]
    public void StrategyChangePlanDoesNotMutateUntilApplied()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("required.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        AddDeclaration(repoNode, fileNode, "DiskA");
        var useCases = CreateUseCases(
            repoRoot,
            TestTreeFactory.Bundle("DiskA", fileRoot));

        var plan = useCases.PlanStrategyChange(
            repoNode,
            DeclareHoldingStrategyType.Default);

        Assert.Single(plan.ValidationFailures);
        Assert.Null(repoNode.DeclareHoldingStrategyType);
        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);

        var result = useCases.ApplyStrategyChange(plan);

        Assert.True(result.Succeeded);
        Assert.Equal(
            DeclareHoldingStrategyType.Default,
            repoNode.DeclareHoldingStrategyType);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
            },
            result.PersistenceTargets);
    }

    [Fact]
    public void StrategyChangeWithoutFailuresOnlyMarksRepository()
    {
        var (useCases, repoNode, _) = CreateMatchingTrees();
        var plan = useCases.PlanStrategyChange(
            repoNode,
            DeclareHoldingStrategyType.Default);

        var result = useCases.ApplyStrategyChange(plan);

        Assert.Empty(plan.ValidationFailures);
        Assert.Equal(
            new[] { PersistenceTarget.Repository },
            result.PersistenceTargets);
    }

    private static (
        DeclarationUseCases UseCases,
        RepoNode RepoNode,
        FileNode FileNode) CreateMatchingTrees()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("movie.mkv")));
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File("Movies", TestTreeFactory.DiskFile("movie.mkv")));
        return (
            CreateUseCases(repoRoot, TestTreeFactory.Bundle("DiskA", fileRoot)),
            (RepoNode)repoRoot.Children[0],
            (FileNode)fileRoot.Children[0]);
    }

    private static DeclarationUseCases CreateUseCases(
        RepoNode repoRoot,
        params FileData[] fileDatas)
    {
        return new DeclarationUseCases(
            new DeclarationSyncService(repoRoot, fileDatas.ToList()));
    }

    private static void AddDeclaration(
        RepoNode repoNode,
        FileNode fileNode,
        string diskLabel)
    {
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = diskLabel,
            FileNodePath = fileNode.GetPath(),
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath(),
        });
    }
}
