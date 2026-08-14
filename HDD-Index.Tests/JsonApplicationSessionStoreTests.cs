using HDD_Index.Application.Persistence;
using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class JsonApplicationSessionStoreTests
{
    [Fact]
    public void Load_CreatesSessionAndRestoresTreeParentReferences()
    {
        using var tempDirectory = new TempDirectory();
        var dataDirectory = Path.Combine(tempDirectory.Path, "data");
        Directory.CreateDirectory(dataDirectory);
        var configPath = Path.Combine(tempDirectory.Path, "config.json");
        var appConfig = new AppConfig
        {
            JsonFilePath = dataDirectory,
            RepoFileName = "repo.json",
            FileDataFiles =
            {
                new FileDataFileConfig
                {
                    JsonFilePath = "disk-a.json",
                    LocalFolderPath = @"C:\DiskA",
                },
            },
        };
        var repoRoot = TestTreeFactory.Repo(
            "Repo",
            TestTreeFactory.Repo("Movies"));
        var fileData = new FileData
        {
            DiskLabel = "DiskA",
            JsonFilePath = Path.Combine(dataDirectory, "disk-a.json"),
            FileNodeRoot = TestTreeFactory.File(
                "DiskA",
                TestTreeFactory.File("Movies")),
        };
        var configService = new AppConfigService();
        var treeDataStore = new TreeDataStore();
        configService.Save(configPath, appConfig);
        treeDataStore.SaveRepoRoot(appConfig, repoRoot);
        treeDataStore.SaveFileData(fileData);
        var store = new JsonApplicationSessionStore(
            configService,
            treeDataStore);

        var session = store.Load(configPath);

        Assert.Equal(configPath, session.AppConfigFilePath);
        Assert.Equal("Repo", session.RepoNodeRoot.Name);
        Assert.Same(
            session.RepoNodeRoot,
            session.RepoNodeRoot.Children[0].Parent);
        var loadedFileData = Assert.Single(session.FileDatas);
        Assert.Equal("disk-a", loadedFileData.DiskLabel);
        Assert.Equal(@"C:\DiskA", loadedFileData.LocalFolderPath);
        Assert.Same(
            loadedFileData.FileNodeRoot,
            loadedFileData.FileNodeRoot.Children[0].Parent);
    }

    [Fact]
    public void Save_AppConfigTargetUsesTheSessionConfigPath()
    {
        using var tempDirectory = new TempDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "custom-config.json");
        var appConfig = new AppConfig
        {
            JsonFilePath = tempDirectory.Path,
            RepoFileName = "repo.json",
        };
        var configService = new AppConfigService();
        var store = new JsonApplicationSessionStore(
            configService,
            new TreeDataStore());
        var session = new ApplicationSession(
            configPath,
            appConfig,
            TestTreeFactory.Repo("Repo"),
            []);

        store.Save(session, PersistenceTarget.AppConfig);

        var loadedConfig = configService.Load(configPath);
        Assert.Equal(tempDirectory.Path, loadedConfig.JsonFilePath);
        Assert.Equal("repo.json", loadedConfig.RepoFileName);
    }

    [Fact]
    public void LoadWithDiagnostics_SkipsMissingIndexAndLoadsHealthyIndexes()
    {
        using var tempDirectory = new TempDirectory();
        var dataDirectory = Path.Combine(tempDirectory.Path, "data");
        Directory.CreateDirectory(dataDirectory);
        var configPath = Path.Combine(tempDirectory.Path, "config.json");
        var appConfig = new AppConfig
        {
            JsonFilePath = dataDirectory,
            RepoFileName = "repo.json",
            FileDataFiles =
            [
                new FileDataFileConfig { JsonFilePath = "healthy.json" },
                new FileDataFileConfig { JsonFilePath = "missing.json" },
            ],
        };
        var configService = new AppConfigService();
        var treeDataStore = new TreeDataStore();
        configService.Save(configPath, appConfig);
        treeDataStore.SaveRepoRoot(appConfig, TestTreeFactory.Repo("Repo"));
        treeDataStore.SaveFileData(new FileData
        {
            DiskLabel = "healthy",
            JsonFilePath = Path.Combine(dataDirectory, "healthy.json"),
            FileNodeRoot = TestTreeFactory.File("Healthy"),
        });
        var store = new JsonApplicationSessionStore(configService, treeDataStore);

        var result = store.LoadWithDiagnostics(configPath);

        Assert.True(result.Succeeded);
        Assert.Equal("healthy", Assert.Single(result.Session!.FileDatas).DiskLabel);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(SessionLoadIssueKind.FileIndexMissing, warning.Kind);
        Assert.Equal("missing", warning.DiskLabel);
        Assert.Equal(2, result.Session.AppConfig.FileDataFiles.Count);
    }

    [Fact]
    public void LoadWithDiagnostics_SkipsInvalidIndexWithoutOverwritingIt()
    {
        using var tempDirectory = new TempDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "config.json");
        var appConfig = new AppConfig
        {
            JsonFilePath = tempDirectory.Path,
            RepoFileName = "repo.json",
            FileDataFiles =
            [
                new FileDataFileConfig { JsonFilePath = "broken.json" },
            ],
        };
        var configService = new AppConfigService();
        var treeDataStore = new TreeDataStore();
        configService.Save(configPath, appConfig);
        treeDataStore.SaveRepoRoot(appConfig, TestTreeFactory.Repo("Repo"));
        var brokenPath = Path.Combine(tempDirectory.Path, "broken.json");
        File.WriteAllText(brokenPath, "{ not json");
        var store = new JsonApplicationSessionStore(configService, treeDataStore);

        var result = store.LoadWithDiagnostics(configPath);
        var manager = new ApplicationSessionManager(result.Session!, store);
        manager.MarkAllFileDataDirty();
        manager.SaveDirtyFiles();

        Assert.True(result.Succeeded);
        Assert.Empty(result.Session!.FileDatas);
        Assert.Equal(SessionLoadIssueKind.FileIndexInvalid, Assert.Single(result.Warnings).Kind);
        Assert.Equal("{ not json", File.ReadAllText(brokenPath));
    }

    [Fact]
    public void LoadWithDiagnostics_InvalidRepositoryBlocksStartup()
    {
        using var tempDirectory = new TempDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "config.json");
        var appConfig = new AppConfig
        {
            JsonFilePath = tempDirectory.Path,
            RepoFileName = "repo.json",
        };
        var configService = new AppConfigService();
        configService.Save(configPath, appConfig);
        File.WriteAllText(Path.Combine(tempDirectory.Path, "repo.json"), "{ not json");
        var store = new JsonApplicationSessionStore(
            configService,
            new TreeDataStore());

        var result = store.LoadWithDiagnostics(configPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        Assert.Equal(
            SessionLoadIssueKind.RepositoryInvalid,
            result.BlockingIssue!.Kind);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"HDD-Index-Session-Tests-{Guid.NewGuid():N}");

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
