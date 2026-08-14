using CommunityToolkit.Mvvm.Input;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.Startup;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class StartupViewModelTests
{
    [Fact]
    public async Task CreateCommand_SelectsDirectoryAndCompletesWithReadySession()
    {
        var configPath = "C:\\Settings\\config.json";
        var firstRun = ApplicationStartupResult.FirstRun(
            configPath,
            new SessionLoadIssue(
                SessionLoadIssueKind.ConfigurationMissing,
                configPath,
                "missing"));
        var ready = CreateReadyResult(configPath);
        var service = new StubStartupService { CreateResult = ready };
        var interaction = new StubStartupInteraction("D:\\IndexData");
        ApplicationStartupResult? completedResult = null;
        var viewModel = new StartupViewModel(
            firstRun,
            service,
            interaction,
            result => completedResult = result);

        await ((IAsyncRelayCommand)viewModel.CreateCommand).ExecuteAsync(null);

        Assert.Equal("D:\\IndexData", service.CreatedDataDirectory);
        Assert.Same(ready, completedResult);
    }

    [Fact]
    public async Task RepairCommandUsesCurrentConfigAndSelectedDirectory()
    {
        var configPath = "C:\\Settings\\config.json";
        var blocked = ApplicationStartupResult.Blocked(
            configPath,
            new SessionLoadIssue(
                SessionLoadIssueKind.DataDirectoryMissing,
                "D:\\OldData",
                "missing"));
        var ready = CreateReadyResult(configPath);
        var service = new StubStartupService { RepairResult = ready };
        var interaction = new StubStartupInteraction("E:\\MovedData");
        ApplicationStartupResult? completedResult = null;
        var viewModel = new StartupViewModel(
            blocked,
            service,
            interaction,
            result => completedResult = result);

        await ((IAsyncRelayCommand)viewModel.RepairDataDirectoryCommand)
            .ExecuteAsync(null);

        Assert.True(viewModel.CanRepairDataDirectory);
        Assert.Equal((configPath, "E:\\MovedData"), service.RepairRequest);
        Assert.Same(ready, completedResult);
    }

    private static ApplicationStartupResult CreateReadyResult(string configPath)
    {
        return ApplicationStartupResult.Ready(
            configPath,
            new ApplicationSession(
                configPath,
                new AppConfig
                {
                    JsonFilePath = "D:\\Data",
                    RepoFileName = "repo.json",
                },
                TestTreeFactory.Repo("Repo"),
                []));
    }

    private sealed class StubStartupInteraction : IStartupInteraction
    {
        private readonly string? _selectedPath;

        public StubStartupInteraction(string? selectedPath)
        {
            _selectedPath = selectedPath;
        }

        public Task<string?> SelectDataDirectoryAsync(string title)
        {
            return Task.FromResult(_selectedPath);
        }
    }

    private sealed class StubStartupService : IApplicationStartupService
    {
        public ApplicationStartupResult CreateResult { get; init; } = null!;

        public ApplicationStartupResult RepairResult { get; init; } = null!;

        public string? CreatedDataDirectory { get; private set; }

        public (string ConfigPath, string DataDirectory)? RepairRequest { get; private set; }

        public ApplicationStartupResult LoadDefault()
        {
            throw new NotSupportedException();
        }

        public ApplicationStartupResult Load(string configFilePath)
        {
            throw new NotSupportedException();
        }

        public ApplicationStartupResult CreateDefault(string dataDirectoryPath)
        {
            CreatedDataDirectory = dataDirectoryPath;
            return CreateResult;
        }

        public ApplicationStartupResult RepairDataDirectory(
            string configFilePath,
            string dataDirectoryPath)
        {
            RepairRequest = (configFilePath, dataDirectoryPath);
            return RepairResult;
        }
    }
}
