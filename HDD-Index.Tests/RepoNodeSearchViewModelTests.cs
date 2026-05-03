using System.Reactive.Linq;
using HDD_Index.Services;
using HDD_Index.ViewModels;
using ReactiveUI;

namespace HDD_Index.Tests;

public class RepoNodeSearchViewModelTests
{
    [Fact]
    public void CreateRepoSource_UsesSingleRowSelection()
    {
        var rootVm = CreateSearchTree();

        var source = TreeDataGridSourceFactory.CreateRepoSource(rootVm);

        Assert.NotNull(source.RowSelection);
        Assert.True(source.RowSelection.SingleSelect);
    }

    [Fact]
    public void FindRepoNodeVmsByNameContains_MatchesFilesAndDirectoriesIgnoringCase()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("MEDIA"),
            TestTreeFactory.RepoFile("movie.mkv"),
            TestTreeFactory.Repo("Books"));
        var rootVm = RepoNodeVM.Create(repoRoot);

        var matches = TreeNavigationService.FindRepoNodeVmsByNameContains(
            rootVm,
            "m");

        Assert.Equal(new[] { "MEDIA", "movie.mkv" }, matches.Select(x => x.Node.Name));
        Assert.Equal(2, matches[0].IndexPath.Count);
        Assert.Equal(0, matches[0].IndexPath[0]);
        Assert.Equal(0, matches[0].IndexPath[1]);
        Assert.False(matches[1].Node.IsDirectory);
    }

    [Fact]
    public void RefreshMatches_SelectsFirstMatchAndUpdatesCounter()
    {
        var rootVm = CreateSearchTree();
        var search = new RepoNodeSearchViewModel
        {
            SearchText = "mov"
        };

        search.RefreshMatches(rootVm);

        Assert.Equal("1/2", search.MatchCounterText);
        Assert.Equal("Movies", search.CurrentMatch?.Node.Name);
        Assert.True(search.HasMatches);
    }

    [Fact]
    public void SearchCommands_WrapAroundMatches()
    {
        var rootVm = CreateSearchTree();
        var search = new RepoNodeSearchViewModel
        {
            SearchText = "mov"
        };
        search.RefreshMatches(rootVm);

        search.SearchPreviousCommand.Execute().Subscribe();
        Assert.Equal("movie.mkv", search.CurrentMatch?.Node.Name);
        Assert.Equal("2/2", search.MatchCounterText);

        search.SearchNextCommand.Execute().Subscribe();
        Assert.Equal("Movies", search.CurrentMatch?.Node.Name);
        Assert.Equal("1/2", search.MatchCounterText);
    }

    [Fact]
    public void DeactivateCurrentMatch_ShowsUnknownNumeratorButKeepsCurrentIndex()
    {
        var rootVm = CreateSearchTree();
        var search = new RepoNodeSearchViewModel
        {
            SearchText = "mov"
        };
        search.RefreshMatches(rootVm);

        search.DeactivateCurrentMatch();

        Assert.Equal("?/2", search.MatchCounterText);
        Assert.Equal(0, search.CurrentMatchIndex);
        Assert.Equal("Movies", search.CurrentMatch?.Node.Name);

        search.SearchNextCommand.Execute().Subscribe();

        Assert.Equal("2/2", search.MatchCounterText);
        Assert.Equal("movie.mkv", search.CurrentMatch?.Node.Name);
        Assert.True(search.IsCurrentMatchActive);
    }

    [Fact]
    public void DeactivateCurrentMatch_DoesNotRaiseCurrentMatchChanged()
    {
        var rootVm = CreateSearchTree();
        var search = new RepoNodeSearchViewModel
        {
            SearchText = "mov"
        };
        search.RefreshMatches(rootVm);
        var currentMatchChangedCount = 0;
        search.WhenAnyValue(x => x.CurrentMatch)
            .Skip(1)
            .Subscribe(_ => currentMatchChangedCount++);

        search.DeactivateCurrentMatch();

        Assert.Equal(0, currentMatchChangedCount);
    }

    [Fact]
    public void SearchNextCommand_FromInactiveSingleMatch_ReactivatesCurrentMatch()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies"),
            TestTreeFactory.Repo("Books"));
        var rootVm = RepoNodeVM.Create(repoRoot);
        var search = new RepoNodeSearchViewModel
        {
            SearchText = "mov"
        };
        search.RefreshMatches(rootVm);
        search.DeactivateCurrentMatch();

        search.SearchNextCommand.Execute().Subscribe();

        Assert.Equal("1/1", search.MatchCounterText);
        Assert.Equal("Movies", search.CurrentMatch?.Node.Name);
        Assert.True(search.IsCurrentMatchActive);
    }

    [Fact]
    public void RefreshMatches_ClearsStateWhenSearchTextIsEmpty()
    {
        var rootVm = CreateSearchTree();
        var search = new RepoNodeSearchViewModel
        {
            SearchText = "mov"
        };
        search.RefreshMatches(rootVm);

        search.SearchText = string.Empty;
        search.RefreshMatches(rootVm);

        Assert.Equal("0/0", search.MatchCounterText);
        Assert.Null(search.CurrentMatch);
        Assert.False(search.HasMatches);
    }

    private static RepoNodeVM CreateSearchTree()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.RepoFile("movie.mkv")),
            TestTreeFactory.Repo("Books"));
        return RepoNodeVM.Create(repoRoot);
    }
}
