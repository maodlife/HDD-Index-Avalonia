using CommunityToolkit.Mvvm.Input;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.Startup;
using HDD_Index.ViewModels;
using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class StartupDialogFlowTests
{
    [Fact]
    public async Task SelectingDirectoryDoesNotWriteUntilContinue()
    {
        var initial = FirstRun();
        var service = new RecordingStartupService(initial);
        var picker = new DirectoryPicker("/data/index");
        var vm = new StartupViewModel(initial, service, picker, _ => { });

        Assert.False(vm.CanContinue);
        vm.ContinueCommand.Execute(null);
        Assert.Equal(0, service.Writes);

        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        Assert.Equal("/data/index", vm.SelectedDataDirectory);
        Assert.True(vm.CanContinue);
        Assert.Equal(0, service.Writes);

        vm.ContinueCommand.Execute(null);
        Assert.Equal("/data/index", service.CreatedPath);
        Assert.Equal(1, service.Writes);
    }

    [Fact]
    public async Task CancellingPickerKeepsSelectionWithoutWriting()
    {
        var initial = FirstRun();
        var service = new RecordingStartupService(initial);
        var picker = new DirectoryPicker("/data/index");
        var vm = new StartupViewModel(initial, service, picker, _ => { });
        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        picker.Path = null;
        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        Assert.Equal("/data/index", vm.SelectedDataDirectory);
        Assert.Equal(0, service.Writes);
    }

    [Fact]
    public async Task ContinueRepairsCurrentConfigurationInsteadOfCreating()
    {
        var blocked = ApplicationStartupResult.Blocked("/config.json",
            new SessionLoadIssue(SessionLoadIssueKind.DataDirectoryMissing, "/old", "missing"));
        var service = new RecordingStartupService(blocked);
        var vm = new StartupViewModel(blocked, service, new DirectoryPicker("/moved"), _ => { });
        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        vm.ContinueCommand.Execute(null);
        Assert.Equal(("/config.json", "/moved"), service.RepairedPath);
        Assert.Null(service.CreatedPath);
        Assert.Contains("missing", vm.Details);
    }

    [Fact]
    public async Task UnrepairableFailureDoesNotAllowDirectorySelectionOrContinue()
    {
        var blocked = ApplicationStartupResult.Blocked("/config.json",
            new SessionLoadIssue(SessionLoadIssueKind.ConfigurationInvalid, "/config.json", "invalid"));
        var service = new RecordingStartupService(blocked);
        var picker = new DirectoryPicker("/data");
        var vm = new StartupViewModel(blocked, service, picker, _ => { });
        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        vm.ContinueCommand.Execute(null);
        Assert.False(vm.CanSelectDataDirectory);
        Assert.False(vm.CanContinue);
        Assert.Equal(0, picker.Requests);
        Assert.Equal(0, service.Writes);
    }

    [Fact]
    public async Task ContinueRepair_RevalidatesDirectoryDeletedAfterSelection()
    {
        using var temp = new TempDirectory();
        var configPath = Path.Combine(temp.Path, "config.json");
        var originalPath = Path.Combine(temp.Path, "old-data");
        var candidatePath = Path.Combine(temp.Path, "candidate");
        Directory.CreateDirectory(candidatePath);
        var configService = new AppConfigService();
        configService.Save(configPath, new AppConfig
        {
            JsonFilePath = originalPath,
            RepoFileName = "RepoTreeData.json",
        });
        var originalConfig = File.ReadAllText(configPath);
        var service = CreateRealService(configPath, configService);
        var completions = 0;
        var vm = new StartupViewModel(service.LoadDefault(), service,
            new DirectoryPicker(candidatePath), _ => completions++);

        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        Directory.Delete(candidatePath);
        vm.ContinueCommand.Execute(null);

        Assert.Equal(0, completions);
        Assert.Equal(originalConfig, File.ReadAllText(configPath));
        Assert.False(Directory.Exists(candidatePath));
        Assert.Contains(candidatePath, vm.Details);
        Assert.True(vm.CanRepairDataDirectory);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ContinueCreate_FileReplacesSelectedDirectoryReportsFailureWithoutConfiguration()
    {
        using var temp = new TempDirectory();
        var configPath = Path.Combine(temp.Path, "config.json");
        var candidatePath = Path.Combine(temp.Path, "candidate");
        Directory.CreateDirectory(candidatePath);
        var service = CreateRealService(configPath);
        var completions = 0;
        var vm = new StartupViewModel(service.LoadDefault(), service,
            new DirectoryPicker(candidatePath), _ => completions++);

        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        Directory.Delete(candidatePath);
        File.WriteAllText(candidatePath, "do not overwrite");
        vm.ContinueCommand.Execute(null);

        Assert.Equal(0, completions);
        Assert.False(File.Exists(configPath));
        Assert.Equal("do not overwrite", File.ReadAllText(candidatePath));
        Assert.Contains(candidatePath, vm.Details);
        Assert.Contains("无法创建", vm.Details);
        Assert.True(vm.CanCreate);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ContinueCreate_RepositoryAppearingAfterSelectionIsNotOverwritten()
    {
        using var temp = new TempDirectory();
        var configPath = Path.Combine(temp.Path, "config.json");
        var candidatePath = Path.Combine(temp.Path, "candidate");
        Directory.CreateDirectory(candidatePath);
        var service = CreateRealService(configPath);
        var completions = 0;
        var vm = new StartupViewModel(service.LoadDefault(), service,
            new DirectoryPicker(candidatePath), _ => completions++);

        await ((IAsyncRelayCommand)vm.SelectDataDirectoryCommand).ExecuteAsync(null);
        var repositoryPath = Path.Combine(candidatePath, "RepoTreeData.json");
        File.WriteAllText(repositoryPath, "existing repository");
        vm.ContinueCommand.Execute(null);

        Assert.Equal(0, completions);
        Assert.False(File.Exists(configPath));
        Assert.Equal("existing repository", File.ReadAllText(repositoryPath));
        Assert.Contains(repositoryPath, vm.Details);
        Assert.Contains("避免覆盖", vm.Details);
        Assert.True(vm.CanCreate);
        Assert.False(vm.IsBusy);
    }

    private static ApplicationStartupService CreateRealService(
        string configPath, AppConfigService? configService = null)
    {
        configService ??= new AppConfigService();
        var treeStore = new TreeDataStore();
        return new ApplicationStartupService(configPath, configService, treeStore,
            new JsonApplicationSessionStore(configService, treeStore));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"HDD-Index-Startup-Dialog-Tests-{Guid.NewGuid():N}");

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private static ApplicationStartupResult FirstRun() => ApplicationStartupResult.FirstRun(
        "/config.json", new SessionLoadIssue(SessionLoadIssueKind.ConfigurationMissing, "/config.json", "missing"));

    private sealed class DirectoryPicker(string? path) : IStartupInteraction
    {
        public string? Path { get; set; } = path;
        public int Requests { get; private set; }

        public Task<string?> SelectDataDirectoryAsync(string title)
        {
            Requests++;
            return Task.FromResult(Path);
        }
    }

    private sealed class RecordingStartupService(ApplicationStartupResult result) : IApplicationStartupService
    {
        public int Writes { get; private set; }
        public string? CreatedPath { get; private set; }
        public (string ConfigPath, string DataPath)? RepairedPath { get; private set; }
        public ApplicationStartupResult LoadDefault() => result;
        public ApplicationStartupResult Load(string configFilePath) => result;

        public ApplicationStartupResult CreateDefault(string dataDirectoryPath)
        {
            Writes++;
            CreatedPath = dataDirectoryPath;
            return result;
        }

        public ApplicationStartupResult RepairDataDirectory(string configFilePath, string dataDirectoryPath)
        {
            Writes++;
            RepairedPath = (configFilePath, dataDirectoryPath);
            return result;
        }
    }
}
