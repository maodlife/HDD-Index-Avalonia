using HDD_Index.Models;
using HDD_Index.Services;
using System.Text.Json;

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
    public void SaveFileData_WritesJsonThatCanBeLoaded()
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
        var fileData = TestTreeFactory.Bundle("DiskA", fileRoot);
        fileData.JsonFilePath = filePath;

        store.SaveFileData(fileData);

        var loaded = store.LoadFileDatas(appConfig).Single();
        Assert.Equal("disk-a", loaded.DiskLabel);
        Assert.Equal(@"C:\DiskA", loaded.LocalFolderPath);
        Assert.Equal(filePath, loaded.JsonFilePath);
        Assert.Equal("Movies", loaded.FileNodeRoot.Children[0].Name);
    }

    [Fact]
    public void LoadFileDatas_WithoutConfiguredFilesUsesLegacyDirectoryDiscovery()
    {
        using var tempDir = new TempDirectory();
        var appConfig = new AppConfig
        {
            JsonFilePath = tempDir.Path,
            RepoFileName = "repo.json",
        };
        var store = new TreeDataStore();
        store.SaveRepoRoot(appConfig, TestTreeFactory.Repo("Repo"));
        store.SaveFileData(CreateFileData(
            "DiskB",
            System.IO.Path.Combine(tempDir.Path, "disk-b.json")));
        store.SaveFileData(CreateFileData(
            "DiskA",
            System.IO.Path.Combine(tempDir.Path, "disk-a.json")));

        var loaded = store.LoadFileDatas(appConfig);

        Assert.Equal(new[] { "disk-a", "disk-b" }, loaded.Select(x => x.DiskLabel));
        Assert.All(loaded, fileData => Assert.Empty(fileData.LocalFolderPath));
    }

    [Fact]
    public void SaveRepoRoot_PreservesPolymorphicJsonContract()
    {
        using var tempDir = new TempDirectory();
        var appConfig = new AppConfig
        {
            JsonFilePath = tempDir.Path,
            RepoFileName = "repo.json",
        };
        var store = new TreeDataStore();
        var repoRoot = TestTreeFactory.Repo(
            "Repo",
            TestTreeFactory.Repo("Movies"));

        store.SaveRepoRoot(appConfig, repoRoot);

        using var document = JsonDocument.Parse(
            File.ReadAllText(System.IO.Path.Combine(tempDir.Path, "repo.json")));
        var child = document.RootElement.GetProperty("Children")[0];
        Assert.Equal("repoNode", child.GetProperty("$type").GetString());
        Assert.False(document.RootElement.TryGetProperty("Parent", out _));
        Assert.False(child.TryGetProperty("Parent", out _));
    }

    private static FileData CreateFileData(
        string diskLabel,
        string jsonFilePath)
    {
        return new FileData
        {
            DiskLabel = diskLabel,
            JsonFilePath = jsonFilePath,
            FileNodeRoot = TestTreeFactory.File(diskLabel),
        };
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
