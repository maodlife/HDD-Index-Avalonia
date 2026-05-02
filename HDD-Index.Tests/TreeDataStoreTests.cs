using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class TreeDataStoreTests
{
    [Fact]
    public void SaveRepoRoot_WritesJsonThatCanBeLoaded()
    {
        using var tempDir = new TempDirectory();
        var appConfig = new AppConfig
        {
            JsonFilePath = tempDir.Path,
            RepoFileName = "repo.json"
        };
        var store = new TreeDataStore();
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies", TestTreeFactory.RepoFile("movie.mkv")));

        store.SaveRepoRoot(appConfig, repoRoot);

        var loadedRoot = store.LoadRepoRoot(appConfig);
        Assert.Equal("Root", loadedRoot.Name);
        Assert.Equal("Movies", loadedRoot.Children[0].Name);
        Assert.Same(loadedRoot, loadedRoot.Children[0].Parent);
    }

    [Fact]
    public void SaveFileDataBundle_WritesJsonThatCanBeLoaded()
    {
        using var tempDir = new TempDirectory();
        var filePath = System.IO.Path.Combine(tempDir.Path, "disk-a.json");
        var appConfig = new AppConfig
        {
            JsonFilePath = tempDir.Path,
            RepoFileName = "repo.json",
            FileDataFiles =
            {
                new FileDataFileConfig
                {
                    JsonFilePath = "disk-a.json",
                    LocalFolderPath = @"C:\DiskA"
                }
            }
        };
        var store = new TreeDataStore();
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File("Movies", TestTreeFactory.DiskFile("movie.mkv")));
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        bundle.FileData.JsonFilePath = filePath;

        store.SaveFileDataBundle(bundle);

        var loadedBundle = store.LoadFileDataVmBundles(appConfig).Single();
        Assert.Equal("disk-a", loadedBundle.FileData.DiskLabel);
        Assert.Equal(@"C:\DiskA", loadedBundle.FileData.LocalFolderPath);
        Assert.Equal(filePath, loadedBundle.FileData.JsonFilePath);
        Assert.Equal("Movies", loadedBundle.FileData.FileNodeRoot.Children[0].Name);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"HDD-Index-Tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
