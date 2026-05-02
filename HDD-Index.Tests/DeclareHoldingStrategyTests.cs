using HDD_Index.Models;

namespace HDD_Index.Tests;

public class DeclareHoldingStrategyTests
{
    [Fact]
    public void DefaultStrategy_ReturnsTrueWhenRepoSubtreeExistsInFileTree()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("movie.mkv"));
        var fileNode = TestTreeFactory.File(
            "Movies",
            TestTreeFactory.DiskFile("movie.mkv"),
            TestTreeFactory.DiskFile("extra.nfo"));
        var strategy = DeclareHoldingStrategyFactory.Create(
            DeclareHoldingStrategyType.Default);

        var result = strategy.CheckDeclareHolding(
            repoNode,
            fileNode,
            out var failureReason);

        Assert.True(result);
        Assert.Empty(failureReason);
    }

    [Fact]
    public void DefaultStrategy_ReturnsFalseWhenRepoFileIsMissing()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("movie.mkv"));
        var fileNode = TestTreeFactory.File("Movies");
        var strategy = DeclareHoldingStrategyFactory.Create(
            DeclareHoldingStrategyType.Default);

        var result = strategy.CheckDeclareHolding(
            repoNode,
            fileNode,
            out var failureReason);

        Assert.False(result);
        Assert.Contains("movie.mkv", failureReason);
    }

    [Fact]
    public void BDRipStrategy_AllowsMissingAssAndTorrentFiles()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("movie.mkv"),
            TestTreeFactory.RepoFile("subtitle.ass"),
            TestTreeFactory.RepoFile("download.torrent"));
        var fileNode = TestTreeFactory.File(
            "Movies",
            TestTreeFactory.DiskFile("movie.mkv"));
        var strategy = DeclareHoldingStrategyFactory.Create(
            DeclareHoldingStrategyType.BDRip);

        Assert.True(strategy.CheckDeclareHolding(repoNode, fileNode, out _));
    }

    [Fact]
    public void BDRipStrategy_AllowsMissingOptionalOnlyDirectory()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("movie.mkv"),
            TestTreeFactory.Repo(
                "Subs",
                TestTreeFactory.RepoFile("subtitle.ass")));
        var fileNode = TestTreeFactory.File(
            "Movies",
            TestTreeFactory.DiskFile("movie.mkv"));
        var strategy = DeclareHoldingStrategyFactory.Create(
            DeclareHoldingStrategyType.BDRip);

        Assert.True(strategy.CheckDeclareHolding(repoNode, fileNode, out _));
    }

    [Fact]
    public void BDRipStrategy_IgnoresOptionalExtensionCase()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("subtitle.ASS"));
        var fileNode = TestTreeFactory.File("Movies");
        var strategy = DeclareHoldingStrategyFactory.Create(
            DeclareHoldingStrategyType.BDRip);

        Assert.True(strategy.CheckDeclareHolding(repoNode, fileNode, out _));
    }

    [Fact]
    public void BDRipStrategy_ReturnsFalseWhenRequiredFileIsMissing()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("movie.mkv"));
        var fileNode = TestTreeFactory.File("Movies");
        var strategy = DeclareHoldingStrategyFactory.Create(
            DeclareHoldingStrategyType.BDRip);

        var result = strategy.CheckDeclareHolding(
            repoNode,
            fileNode,
            out var failureReason);

        Assert.False(result);
        Assert.Contains("movie.mkv", failureReason);
    }
}
