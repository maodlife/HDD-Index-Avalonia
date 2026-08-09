using HDD_Index.Application.Persistence;
using HDD_Index.Application.Repositories;
using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class RepositoryUseCasesTests
{
    [Fact]
    public void CreateChildFolderReturnsPreferredNodeAndAllCurrentPersistenceTargets()
    {
        var root = TestTreeFactory.Repo("Root");
        var fileDatas = new List<FileData>
        {
            TestTreeFactory.Bundle("DiskA", TestTreeFactory.File("DiskA")),
        };
        var useCases = CreateUseCases(root, fileDatas);
        fileDatas.Add(
            TestTreeFactory.Bundle("DiskB", TestTreeFactory.File("DiskB")));

        var result = useCases.CreateChildFolder(root);

        Assert.True(result.Succeeded);
        Assert.Same(root.Children.Single(), result.PreferredNode);
        Assert.Equal("新建文件夹", result.PreferredNode!.Name);
        Assert.False(result.Changes.IsEmpty);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
                PersistenceTarget.ForFileData("DiskB"),
            },
            result.PersistenceTargets);
    }

    [Fact]
    public void CopyFileNodeSubtreeOnlyMarksRepository()
    {
        var root = TestTreeFactory.Repo("Root");
        var source = TestTreeFactory.File(
            "Movies",
            TestTreeFactory.DiskFile("movie.mkv"));
        var useCases = CreateUseCases(
            root,
            [TestTreeFactory.Bundle("DiskA", TestTreeFactory.File("Disk"))]);

        var result = useCases.CopyFileNodeSubtreeToRepoDirectory(root, source);

        Assert.True(result.Succeeded);
        Assert.Same(root.Children.Single(), result.PreferredNode);
        Assert.Equal("movie.mkv", result.PreferredNode!.Children.Single().Name);
        Assert.Equal(
            new[] { PersistenceTarget.Repository },
            result.PersistenceTargets);
    }

    [Fact]
    public void RenameRepoNodeOnConflictFailsWithoutMutationOrPersistenceTargets()
    {
        var first = TestTreeFactory.Repo("First");
        var root = TestTreeFactory.Repo(
            "Root",
            first,
            TestTreeFactory.Repo("Second"));
        var useCases = CreateUseCases(root, []);

        var result = useCases.RenameRepoNode(first, "Second");

        Assert.False(result.Succeeded);
        Assert.Equal("First", first.Name);
        Assert.True(result.Changes.IsEmpty);
        Assert.Empty(result.PersistenceTargets);
        Assert.Null(result.PreferredNode);
    }

    [Fact]
    public void RenameRepoNodeReturnsRenamedNodeAndAllPersistenceTargets()
    {
        var node = TestTreeFactory.Repo("Movies");
        var root = TestTreeFactory.Repo("Root", node);
        var useCases = CreateUseCases(
            root,
            [TestTreeFactory.Bundle("DiskA", TestTreeFactory.File("Disk"))]);

        var result = useCases.RenameRepoNode(node, "Films");

        Assert.True(result.Succeeded);
        Assert.Same(node, result.PreferredNode);
        Assert.Equal("Films", node.Name);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
            },
            result.PersistenceTargets);
    }

    [Fact]
    public void DeleteRepoNodeRejectsRootWithoutPersistenceTargets()
    {
        var root = TestTreeFactory.Repo("Root");
        var useCases = CreateUseCases(root, []);

        var result = useCases.DeleteRepoNode(root, root);

        Assert.False(result.Succeeded);
        Assert.True(result.Changes.IsEmpty);
        Assert.Empty(result.PersistenceTargets);
    }

    [Fact]
    public void DeleteRepoNodeReturnsChangesAndAllPersistenceTargets()
    {
        var child = TestTreeFactory.Repo("Movies");
        var root = TestTreeFactory.Repo("Root", child);
        var useCases = CreateUseCases(
            root,
            [TestTreeFactory.Bundle("DiskA", TestTreeFactory.File("Disk"))]);

        var result = useCases.DeleteRepoNode(child, root);

        Assert.True(result.Succeeded);
        Assert.Empty(root.Children);
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
    public void SearchDeletePlanDoesNotMutateUntilApplied()
    {
        var nestedMatch = TestTreeFactory.Repo("Target");
        var outerMatch = TestTreeFactory.Repo("Target", nestedMatch);
        var selected = TestTreeFactory.Repo("Selected", outerMatch);
        var root = TestTreeFactory.Repo("Root", selected);
        var useCases = CreateUseCases(
            root,
            [TestTreeFactory.Bundle("DiskA", TestTreeFactory.File("Disk"))]);

        var plan = useCases.PlanSearchDelete(selected, root, "target");

        Assert.True(plan.HasMatches);
        Assert.Equal(2, plan.MatchedNodes.Count);
        Assert.Equal(
            new[]
            {
                "Root/Selected/Target",
                "Root/Selected/Target/Target",
            },
            plan.MatchedNodePaths);
        Assert.Single(selected.Children);

        var result = useCases.ApplySearchDelete(plan);

        Assert.True(result.Succeeded);
        Assert.Empty(selected.Children);
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
    public void SearchDeleteWithoutMatchesFailsWithoutPersistenceTargets()
    {
        var selected = TestTreeFactory.Repo("Selected");
        var root = TestTreeFactory.Repo("Root", selected);
        var useCases = CreateUseCases(root, []);
        var plan = useCases.PlanSearchDelete(selected, root, "Missing");

        var result = useCases.ApplySearchDelete(plan);

        Assert.False(plan.HasMatches);
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.FailureReason);
        Assert.True(result.Changes.IsEmpty);
        Assert.Empty(result.PersistenceTargets);
    }

    private static RepositoryUseCases CreateUseCases(
        RepoNode repoRoot,
        List<FileData> fileDatas)
    {
        var declarationService = new DeclarationSyncService(repoRoot, fileDatas);
        return new RepositoryUseCases(
            new RepoTreeEditor(declarationService),
            fileDatas);
    }
}
