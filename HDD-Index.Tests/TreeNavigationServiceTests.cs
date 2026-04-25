using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class TreeNavigationServiceTests
{
    [Fact]
    public void ReplacePathPrefix_ReplacesExactPath()
    {
        var result = TreeNavigationService.ReplacePathPrefix(
            "Root/Movies",
            "Root/Movies",
            "Root/Films");

        Assert.Equal("Root/Films", result);
    }

    [Fact]
    public void ReplacePathPrefix_ReplacesDescendantPath()
    {
        var result = TreeNavigationService.ReplacePathPrefix(
            "Root/Movies/Anime",
            "Root/Movies",
            "Root/Films");

        Assert.Equal("Root/Films/Anime", result);
    }

    [Fact]
    public void ReplacePathPrefix_DoesNotReplacePartialSegment()
    {
        var result = TreeNavigationService.ReplacePathPrefix(
            "Root/MoviesArchive/Anime",
            "Root/Movies",
            "Root/Films");

        Assert.Equal("Root/MoviesArchive/Anime", result);
    }

    [Fact]
    public void FindRepoNodeVmByPath_ReturnsNodeAndIndexPath()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies"),
            TestTreeFactory.Repo(
                "Books",
                TestTreeFactory.Repo("SciFi")));
        var rootVm = RepoNodeVM.Create(repoRoot);

        var result = TreeNavigationService.FindRepoNodeVmByPath(
            rootVm,
            "Root/Books/SciFi",
            out var indexPath);

        Assert.NotNull(result);
        Assert.Equal("SciFi", result.Name);
        Assert.NotNull(indexPath);
        Assert.Equal(3, indexPath.Value.Count);
        Assert.Equal(0, indexPath.Value[0]);
        Assert.Equal(1, indexPath.Value[1]);
        Assert.Equal(0, indexPath.Value[2]);
    }
}
