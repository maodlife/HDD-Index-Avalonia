using System.Reactive.Linq;
using Avalonia.Controls;
using HDD_Index.Application.Declarations;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Application.FileScanning;
using HDD_Index.Application.FileTrees;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.Repositories;
using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class WorkspaceViewModelTests
{
    [Theory]
    [InlineData("Archive 01", "Archive 01/Animation/Film", "Animation/Film")]
    [InlineData("Archive 01", "DifferentRoot/Animation/Film", "DifferentRoot/Animation/Film")]
    [InlineData("Archive", "Archive 01/Film", "Archive 01/Film")]
    public void StorageLocation_DisplayPathOnlyOmitsTheExactDiskLabel(string disk, string path, string expected)
    {
        var location = new StorageLocationViewModel(disk, path);
        Assert.Equal(path, location.FileNodePath);
        Assert.Equal(expected, location.DisplayPath);
    }

    [Fact]
    public void Inspector_UsesExistingProjectionAndRefreshesSelectedNodeDetails()
    {
        var node = TestTreeFactory.Repo("Movies");
        var root = TestTreeFactory.Repo("Repository", node);
        var projection = new TreeProjection();
        var browser = new RepoBrowserViewModel(root, projection);
        browser.UpdateCurrentRepoNode(node);
        var selectedVm = browser.CurrentRepoNode;

        node.Name = "Films";
        node.DeclareHoldingStrategyType = DeclareHoldingStrategyType.BDRip;
        node.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "Archive 01",
            FileNodePath = "Disk/Animation/Films"
        });
        browser.UpdateCurrentRepoNode(node);

        Assert.Same(projection.GetRepoNodeVm(node), selectedVm);
        Assert.Same(selectedVm, browser.CurrentRepoNode);
        Assert.True(browser.HasCurrentRepoNode);
        Assert.Equal("Films", browser.CurrentRepoNodeName);
        Assert.Equal("虚拟文件夹", browser.CurrentRepoNodeKind);
        Assert.Equal("BDRip", browser.CurrentRepoNodeStrategyName);
        Assert.Equal("Repository/Films", browser.RepoNodePathString);
        Assert.Equal(1, browser.StorageLocationCount);
        var location = Assert.Single(browser.CurrentStorageLocations);
        Assert.Equal("Archive 01", location.DiskLabel);
        Assert.Equal("Disk/Animation/Films", location.FileNodePath);
    }

    [Fact]
    public void Inspector_ClearRemovesAllStaleDetailsAndLocations()
    {
        var node = TestTreeFactory.RepoFile("movie.mkv");
        node.SaveFileNodeDatas.Add(new SaveFileNodeData { DiskLabel = "Archive" });
        var browser = new RepoBrowserViewModel(node, new TreeProjection());
        browser.UpdateCurrentRepoNode(node);
        Assert.Equal("虚拟文件", browser.CurrentRepoNodeKind);
        var changed = new List<string?>();
        browser.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        browser.ClearCurrentRepoNode();

        Assert.Null(browser.CurrentRepoNode);
        Assert.False(browser.HasCurrentRepoNode);
        Assert.Empty(browser.CurrentStorageLocations);
        Assert.Empty(browser.CurrRepoNodeSaveFileNodes);
        Assert.Empty(browser.SelectedSaveFileNodeLabel);
        Assert.Empty(browser.RepoNodePathString);
        Assert.Equal(0, browser.StorageLocationCount);
        Assert.Equal("未设置", browser.CurrentRepoNodeStrategyName);
        Assert.Contains(nameof(RepoBrowserViewModel.CurrentRepoNodeName), changed);
        Assert.Contains(nameof(RepoBrowserViewModel.StorageLocationCount), changed);
    }

    [Fact]
    public void SidebarAndModeCommands_KeepExactlyOneBrowseDestinationVisible()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        Assert.True(vm.IsRepositoryBrowseVisible);
        Assert.False(vm.IsDiskBrowseVisible);
        Assert.Null(vm.SelectedSidebarDiskLabel);

        vm.SelectDiskCommand.Execute("Archive 02").Subscribe();
        Assert.False(vm.IsRepositoryBrowseVisible);
        Assert.True(vm.IsDiskBrowseVisible);
        Assert.Equal("Archive 02", vm.FileBrowser.SelectedDiskLabel);
        Assert.Equal("Archive 02", vm.SelectedSidebarDiskLabel);

        vm.ShowOrganizeCommand.Execute().Subscribe();
        Assert.True(vm.IsOrganizeMode);
        Assert.False(vm.IsRepositoryBrowseVisible);
        Assert.False(vm.IsDiskBrowseVisible);

        vm.SelectRepositoryCommand.Execute().Subscribe();
        Assert.Null(vm.SelectedSidebarDiskLabel);
        Assert.Equal("Archive 02", vm.FileBrowser.SelectedDiskLabel);
        vm.ShowBrowseCommand.Execute().Subscribe();
        Assert.True(vm.IsRepositoryBrowseVisible);
        Assert.False(vm.IsDiskBrowseVisible);
    }

    [Fact]
    public void SidebarSelection_SynchronizesWithDiskPickerWithoutClearingIndexSelection()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        vm.SelectedSidebarDiskLabel = "Archive 02";
        Assert.True(vm.IsDiskBrowseVisible);
        Assert.Equal("Archive 02", vm.FileBrowser.SelectedDiskLabel);

        vm.FileBrowser.SelectedDiskLabel = "Archive 01";
        Assert.Equal("Archive 01", vm.SelectedSidebarDiskLabel);
        vm.SelectedSidebarDiskLabel = "Missing";
        Assert.Equal("Archive 01", vm.SelectedSidebarDiskLabel);

        vm.SelectRepositoryCommand.Execute().Subscribe();
        vm.SelectedSidebarDiskLabel = null;
        Assert.True(vm.IsRepositorySelected);
        Assert.Equal("Archive 01", vm.FileBrowser.SelectedDiskLabel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RepositorySearch_RevealsRepositoryAndPreservesWorkspaceMode(int mode)
    {
        var vm = new Fixture().ViewModel;
        vm.ViewModeTabIndex = mode;
        vm.SelectedSidebarDiskLabel = "Archive 02";

        vm.RepoBrowser.RepoSearch.SearchText = "Movies";

        Assert.True(vm.IsRepositorySelected);
        Assert.Equal(mode, vm.ViewModeTabIndex);
        Assert.Equal("Movies", vm.RepoBrowser.CurrentRepoNodeName);
        Assert.Equal("1/1", vm.RepoBrowser.RepoSearch.MatchCounterText);
    }

    [Fact]
    public void ShowStorageLocation_SelectsExactPathAndDoesNotModifyMetadata()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        var node = fixture.RepoChild;
        node.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "Archive 02",
            FileNodePath = "Disk 2/Other"
        });
        vm.RepoBrowser.RepoNodeSource.RowSelection!.Select(new IndexPath(0, 0));

        vm.ShowStorageLocationCommand.Execute(
            new StorageLocationViewModel("Archive 02", "Disk 2/Other")).Subscribe();

        Assert.True(vm.IsOrganizeMode);
        Assert.Equal("Archive 02", vm.FileBrowser.SelectedDiskLabel);
        Assert.Equal("Other", vm.FileBrowser.CurrFileNodeSource.RowSelection!.SelectedItem!.Name);
        Assert.False(vm.HasDirtyFiles);
        Assert.Single(node.SaveFileNodeDatas);
    }

    [Fact]
    public void ShowStorageLocation_IgnoresStaleInspectorActions()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        vm.RepoBrowser.RepoNodeSource.RowSelection!.Select(new IndexPath(0, 0));

        vm.ShowStorageLocationCommand.Execute(
            new StorageLocationViewModel("Archive 02", "Disk 2/Other")).Subscribe();

        Assert.True(vm.IsBrowseMode);
        Assert.Equal("Archive 01", vm.FileBrowser.SelectedDiskLabel);
        Assert.False(vm.HasDirtyFiles);
    }

    [Fact]
    public void OrganizeActions_RequireBothSelectionsAndCopyRequiresDirectory()
    {
        var vm = new Fixture().ViewModel;
        vm.ViewModeTabIndex = 1;
        Assert.False(vm.CanLinkSelectedNodes);
        Assert.False(vm.CanCopySelectedStructure);

        vm.FileBrowser.CurrFileNodeSource.RowSelection!.Select(new IndexPath(0, 0));
        Assert.False(vm.CanLinkSelectedNodes);
        vm.RepoBrowser.RepoNodeSource.RowSelection!.Select(new IndexPath(0, 0));
        Assert.True(vm.CanLinkSelectedNodes);
        Assert.True(vm.CanCopySelectedStructure);

        vm.RepoBrowser.RepoNodeSource.RowSelection.Select(new IndexPath(0, 1));
        Assert.True(vm.CanLinkSelectedNodes);
        Assert.False(vm.CanCopySelectedStructure);
        Assert.False(vm.RepoBrowser.CurrentRepoNodeIsDirectory);
        vm.RepoBrowser.RepoNodeSource.RowSelection.Clear();
        Assert.False(vm.CanLinkSelectedNodes);
        Assert.False(vm.CanCopySelectedStructure);
        Assert.False(vm.RepoBrowser.HasCurrentRepoNode);
    }

    [Fact]
    public void DiskAutoJump_KeepsDiskVisibleButExplicitJumpRevealsRepository()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        var fileNode = vm.FileBrowser.FileDatas[0].FileNodeRoot.Children.OfType<FileNode>().First();
        fixture.RepoChild.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "Archive 01",
            FileNodePath = fileNode.GetPath()
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = fixture.RepoChild.GetPath()
        });
        vm.SelectedSidebarDiskLabel = "Archive 01";
        vm.FileBrowser.CurrFileNodeSource.RowSelection!.Select(new IndexPath(0, 0));

        Assert.True(vm.IsDiskBrowseVisible);
        Assert.Equal("Movies", vm.RepoBrowser.CurrentRepoNodeName);

        vm.RepositoryEditor.JumpToDeclareRepoNodeCommand.Execute(
            vm.FileBrowser.CurrFileNodeSource.RowSelection.SelectedItem!).Subscribe();
        Assert.True(vm.IsRepositoryBrowseVisible);
        Assert.False(vm.HasDirtyFiles);
    }

    [Fact]
    public async Task EstablishAssociation_RefreshesInspectorAndMarksMetadataDirty()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        fixture.RepoChild.DeclareHoldingStrategyType = DeclareHoldingStrategyType.Default;
        vm.ViewModeTabIndex = 1;
        vm.RepoBrowser.RepoNodeSource.RowSelection!.Select(new IndexPath(0, 0));
        vm.FileBrowser.CurrFileNodeSource.RowSelection!.Select(new IndexPath(0, 0));

        await vm.RepositoryEditor.DeclareSelectedRepoNodeCommand.Execute(
            vm.FileBrowser.CurrFileNodeSource.RowSelection.SelectedItem!);

        var location = Assert.Single(vm.RepoBrowser.CurrentStorageLocations);
        Assert.Equal("Archive 01", location.DiskLabel);
        Assert.Equal("Disk 1/Movies", location.FileNodePath);
        Assert.Equal(1, vm.RepoBrowser.StorageLocationCount);
        Assert.Equal("默认", vm.RepoBrowser.CurrentRepoNodeStrategyName);
        Assert.Equal("未保存", vm.SaveStatusText);
    }

    [Fact]
    public async Task RenameAndDelete_UpdateInspectorAndSaveStatus()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        vm.ViewModeTabIndex = 1;
        vm.RepoBrowser.RepoNodeSource.RowSelection!.Select(new IndexPath(0, 0));
        var selected = vm.RepoBrowser.CurrentRepoNode!;
        fixture.Interaction.Rename = "Films";

        await vm.RepositoryEditor.RenameRepoNodeCommand.Execute(selected);

        Assert.Same(selected, vm.RepoBrowser.CurrentRepoNode);
        Assert.Equal("Films", vm.RepoBrowser.CurrentRepoNodeName);
        Assert.Equal("Repository/Films", vm.RepoBrowser.RepoNodePathString);
        Assert.Equal("未保存", vm.SaveStatusText);
        Assert.True(await vm.SaveDirtyFilesAsync());
        Assert.Equal("已保存", vm.SaveStatusText);
        Assert.False(vm.HasDirtyFiles);

        await vm.RepositoryEditor.DeleteRepoNodeCommand.Execute(selected);

        Assert.Null(vm.RepoBrowser.CurrentRepoNode);
        Assert.False(vm.RepoBrowser.HasCurrentRepoNode);
        Assert.Empty(vm.RepoBrowser.CurrentStorageLocations);
        Assert.Equal("未保存", vm.SaveStatusText);
    }

    [Fact]
    public async Task SaveFailure_PreservesDirtyStatusAndReportsFailure()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        vm.RepositoryEditor.CreateChildFolderCommand.Execute(vm.RepoBrowser.RepoNodeVm).Subscribe();
        fixture.Store.FailSave = true;

        Assert.False(await vm.SaveDirtyFilesAsync());
        Assert.True(vm.HasDirtyFiles);
        Assert.Equal("未保存", vm.SaveStatusText);
        Assert.Contains("保存 JSON 文件失败", Assert.Single(fixture.Interaction.Messages));
    }

    [Fact]
    public async Task StrategyCancellation_LeavesInspectorAndMetadataUnchanged()
    {
        var fixture = new Fixture();
        var vm = fixture.ViewModel;
        vm.RepoBrowser.RepoNodeSource.RowSelection!.Select(new IndexPath(0, 0));

        await vm.RepositoryEditor.ChangeDeclareHoldingStrategyCommand.Execute(vm.RepoBrowser.CurrentRepoNode!);
        await vm.RepositoryEditor.ChangeDeclareHoldingStrategyCommand.Execute(vm.RepoBrowser.CurrentRepoNode!);

        Assert.Null(fixture.RepoChild.DeclareHoldingStrategyType);
        Assert.Equal("未设置", vm.RepoBrowser.CurrentRepoNodeStrategyName);
        Assert.False(vm.HasDirtyFiles);
    }

    private sealed class Fixture
    {
        public RepoNode RepoChild { get; } = TestTreeFactory.Repo("Movies");
        public TestInteraction Interaction { get; } = new();
        public TestStore Store { get; } = new();
        public MainWindowViewModel ViewModel { get; }

        public Fixture()
        {
            var root = TestTreeFactory.Repo("Repository", RepoChild, TestTreeFactory.RepoFile("readme.txt"));
            var fileDatas = new List<FileData>
            {
                TestTreeFactory.Bundle("Archive 01", TestTreeFactory.File("Disk 1", TestTreeFactory.File("Movies"))),
                TestTreeFactory.Bundle("Archive 02", TestTreeFactory.File("Disk 2", TestTreeFactory.File("Other")))
            };
            var session = new ApplicationSession("config.json", new AppConfig(), root, fileDatas);
            var projection = new TreeProjection();
            var declarations = new DeclarationSyncService(root, fileDatas);
            ViewModel = new MainWindowViewModel(
                new ApplicationSessionManager(session, Store),
                new DeclarationUseCases(declarations),
                new RepositoryUseCases(new RepoTreeEditor(declarations), fileDatas),
                new FileTreeUseCases(session, new FileTreeEditor(declarations), new FileTreeScanner(), new FileTreePathService()),
                projection,
                new RepoBrowserViewModel(root, projection),
                new FileBrowserViewModel(fileDatas, projection),
                new RepositoryEditorViewModel(),
                Interaction, Interaction, Interaction, Interaction, Interaction);
        }
    }

    private sealed class TestStore : IApplicationSessionStore
    {
        public bool FailSave { get; set; }
        public ApplicationSession LoadDefault() => throw new NotSupportedException();
        public string GetFilePath(ApplicationSession session, PersistenceTarget target) => target.Kind + ".json";
        public void Save(ApplicationSession session, PersistenceTarget target)
        {
            if (FailSave)
                throw new IOException("Test save failure");
        }
    }

    private sealed class TestInteraction : IUserInteraction, IRepositoryInteraction, IFileTreeInteraction,
        IFileTreeScanProgressRunner, IPathOpener
    {
        public string? Rename { get; set; }
        public List<string> Messages { get; } = new();
        public Task ShowMessageAsync(MessageRequest request)
        {
            Messages.Add(request.Message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(ConfirmationRequest request) => Task.FromResult(true);
        public Task<DeclareHoldingStrategySelection> SelectInitialDeclareHoldingStrategyAsync(
            IReadOnlyList<DeclareHoldingStrategyOption> options) => Task.FromResult(new DeclareHoldingStrategySelection(false, null));
        public Task<DeclareHoldingStrategySelection> SelectReplacementDeclareHoldingStrategyAsync(
            IReadOnlyList<DeclareHoldingStrategyOption> options, DeclareHoldingStrategyType? selectedStrategyType)
            => Task.FromResult(new DeclareHoldingStrategySelection(false, null));
        public Task<IReadOnlyList<string>?> SelectDeclareHoldingsToAbandonAsync(IReadOnlyList<string> repoNodePaths)
            => Task.FromResult<IReadOnlyList<string>?>(null);
        public Task<string?> RequestRenameAsync(string initialName) => Task.FromResult(Rename);
        public Task<bool> ConfirmDeleteAsync(string targetName) => Task.FromResult(true);
        public Task<string?> RequestDeleteSearchAsync() => Task.FromResult<string?>(null);
        public Task<bool> ConfirmDeleteMatchesAsync(IReadOnlyList<string> matchedNodePaths) => Task.FromResult(true);
        public Task<NewFileTreeSelection?> RequestNewFileTreeAsync() => Task.FromResult<NewFileTreeSelection?>(null);
        public Task<string?> RequestLocalFolderPathAsync(string diskLabel, string currentPath) => Task.FromResult<string?>(null);
        public Task<FileTreeScanExecutionResult<T>> RunAsync<T>(Func<IProgress<FileTreeScanProgress>, CancellationToken, T> scan)
            => throw new NotSupportedException();
        public void OpenFolder(string folderPath) { }
        public void ShowPathInFolder(string path) { }
    }
}
