using HDD_Index.Application.Persistence;
using HDD_Index.Application.Startup;
using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class ApplicationStartupServiceTests
{
    [Fact]
    public void LoadDefault_MissingConfigurationReturnsFirstRun()
    {
        using var tempDirectory = new TempDirectory();
        var service = CreateService(
            Path.Combine(tempDirectory.Path, "settings", "config.json"));

        var result = service.LoadDefault();

        Assert.Equal(ApplicationStartupState.FirstRun, result.State);
        Assert.Equal(
            SessionLoadIssueKind.ConfigurationMissing,
            result.BlockingIssue!.Kind);
    }

    [Fact]
    public void CreateDefault_CreatesConfigurationAndEmptyRepository()
    {
        using var tempDirectory = new TempDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "settings", "config.json");
        var dataPath = Path.Combine(tempDirectory.Path, "data");
        var service = CreateService(configPath);

        var result = service.CreateDefault(dataPath);

        Assert.Equal(ApplicationStartupState.Ready, result.State);
        Assert.NotNull(result.Session);
        Assert.Equal("Repository", result.Session!.RepoNodeRoot.Name);
        Assert.True(result.Session.RepoNodeRoot.IsDirectory);
        Assert.Empty(result.Session.FileDatas);
        Assert.True(File.Exists(configPath));
        Assert.True(File.Exists(Path.Combine(dataPath, "RepoTreeData.json")));
    }

    [Fact]
    public void CreateDefault_DoesNotOverwriteExistingRepository()
    {
        using var tempDirectory = new TempDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "settings", "config.json");
        var repositoryPath = Path.Combine(tempDirectory.Path, "RepoTreeData.json");
        File.WriteAllText(repositoryPath, "existing data");
        var service = CreateService(configPath);

        var result = service.CreateDefault(tempDirectory.Path);

        Assert.Equal(ApplicationStartupState.Blocked, result.State);
        Assert.Equal("existing data", File.ReadAllText(repositoryPath));
        Assert.False(File.Exists(configPath));
    }

    [Fact]
    public void RepairDataDirectory_ValidatesThenPersistsNewPath()
    {
        using var tempDirectory = new TempDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "config.json");
        var missingDataPath = Path.Combine(tempDirectory.Path, "old-data");
        var newDataPath = Path.Combine(tempDirectory.Path, "new-data");
        Directory.CreateDirectory(newDataPath);
        var configService = new AppConfigService();
        var treeDataStore = new TreeDataStore();
        var config = new AppConfig
        {
            JsonFilePath = missingDataPath,
            RepoFileName = "repo.json",
        };
        configService.Save(configPath, config);
        treeDataStore.SaveRepoRoot(
            new AppConfig
            {
                JsonFilePath = newDataPath,
                RepoFileName = "repo.json",
            },
            TestTreeFactory.Repo("Migrated Repo"));
        var service = CreateService(configPath, configService, treeDataStore);

        var result = service.RepairDataDirectory(configPath, newDataPath);

        Assert.Equal(ApplicationStartupState.Ready, result.State);
        Assert.Equal("Migrated Repo", result.Session!.RepoNodeRoot.Name);
        Assert.Equal(newDataPath, configService.Load(configPath).JsonFilePath);
    }

    [Fact]
    public void RepairDataDirectory_InvalidCandidateKeepsOriginalConfiguration()
    {
        using var tempDirectory = new TempDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "config.json");
        var originalPath = Path.Combine(tempDirectory.Path, "old-data");
        var invalidCandidatePath = Path.Combine(tempDirectory.Path, "empty-data");
        Directory.CreateDirectory(invalidCandidatePath);
        var configService = new AppConfigService();
        configService.Save(configPath, new AppConfig
        {
            JsonFilePath = originalPath,
            RepoFileName = "repo.json",
        });
        var service = CreateService(configPath, configService, new TreeDataStore());

        var result = service.RepairDataDirectory(configPath, invalidCandidatePath);

        Assert.Equal(ApplicationStartupState.Blocked, result.State);
        Assert.Equal(originalPath, configService.Load(configPath).JsonFilePath);
    }

    private static ApplicationStartupService CreateService(
        string configPath,
        AppConfigService? configService = null,
        TreeDataStore? treeDataStore = null)
    {
        configService ??= new AppConfigService();
        treeDataStore ??= new TreeDataStore();
        var sessionStore = new JsonApplicationSessionStore(
            configService,
            treeDataStore);
        return new ApplicationStartupService(
            configPath,
            configService,
            treeDataStore,
            sessionStore);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"HDD-Index-Startup-Tests-{Guid.NewGuid():N}");

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
