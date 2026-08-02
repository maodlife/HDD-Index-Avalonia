using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData.Kernel;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Application.FileScanning;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Messages;
using HDD_Index.Models;
using HDD_Index.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AppConfigService _appConfigService;
    private readonly TreeDataStore _treeDataStore;
    private readonly DirtyJsonFileTracker _dirtyJsonFileTracker;
    private readonly DeclarationSyncService _declarationSyncService;
    private readonly RepoTreeEditor _repoTreeEditor;
    private readonly FileTreeEditor _fileTreeEditor;
    private readonly IFileTreeScanner _fileTreeScanner;
    private readonly TreeProjection _treeProjection;
    private readonly IUserInteraction _userInteraction;
    private readonly IRepositoryInteraction _repositoryInteraction;
    private readonly IFileTreeInteraction _fileTreeInteraction;
    private readonly IFileTreeScanProgressRunner _fileTreeScanProgressRunner;
    private readonly IPathOpener _pathOpener;
    private readonly AppConfig _appConfig;
    private bool _isSelectingRepoRowProgrammatically;

    public RepoBrowserViewModel RepoBrowser { get; }
    public FileBrowserViewModel FileBrowser { get; }
    public RepositoryEditorViewModel RepositoryEditor { get; }

    [Reactive] public int ViewModeTabIndex { get; set; }
    [Reactive] public bool HasDirtyFiles { get; set; }
    [Reactive] public bool IsFileTreeScanRunning { get; set; }

    public bool IsViewMode => ViewModeTabIndex == 0;
    public bool IsEditMode => ViewModeTabIndex == 1;
    public ReactiveCommand<Unit, Unit> SaveCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> OpenCreateNewFileTreeDialogCommand { get; private set; } = null!;

    public MainWindowViewModel(
        AppConfig appConfig,
        AppConfigService appConfigService,
        TreeDataStore treeDataStore,
        DirtyJsonFileTracker dirtyJsonFileTracker,
        DeclarationSyncService declarationSyncService,
        RepoTreeEditor repoTreeEditor,
        FileTreeEditor fileTreeEditor,
        IFileTreeScanner fileTreeScanner,
        TreeProjection treeProjection,
        RepoBrowserViewModel repoBrowser,
        FileBrowserViewModel fileBrowser,
        RepositoryEditorViewModel repositoryEditor,
        IUserInteraction userInteraction,
        IRepositoryInteraction repositoryInteraction,
        IFileTreeInteraction fileTreeInteraction,
        IFileTreeScanProgressRunner fileTreeScanProgressRunner,
        IPathOpener pathOpener)
    {
        _appConfig = appConfig;
        _appConfigService = appConfigService;
        _treeDataStore = treeDataStore;
        _dirtyJsonFileTracker = dirtyJsonFileTracker;
        _declarationSyncService = declarationSyncService;
        _repoTreeEditor = repoTreeEditor;
        _fileTreeEditor = fileTreeEditor;
        _fileTreeScanner = fileTreeScanner;
        _treeProjection = treeProjection;
        RepoBrowser = repoBrowser;
        FileBrowser = fileBrowser;
        RepositoryEditor = repositoryEditor;
        _userInteraction = userInteraction;
        _repositoryInteraction = repositoryInteraction;
        _fileTreeInteraction = fileTreeInteraction;
        _fileTreeScanProgressRunner = fileTreeScanProgressRunner;
        _pathOpener = pathOpener;
        InitDirtyTracker();
        InitCommand();
    }

    private void InitDirtyTracker()
    {
        _dirtyJsonFileTracker.SetAppConfigPath(_appConfigService.GetDefaultConfigPath());
        _dirtyJsonFileTracker.SetRepoFilePath(_treeDataStore.GetRepoFilePath(_appConfig));
        foreach (var fileData in FileBrowser.FileDatas)
        {
            _dirtyJsonFileTracker.SetFileNodePath(
                fileData.DiskLabel,
                fileData.JsonFilePath);
        }
    }

    private void InitCommand()
    {
        SaveCommand = ReactiveCommand.CreateFromTask(
            async () => { await SaveDirtyFilesAsync(); },
            this.WhenAnyValue(x => x.HasDirtyFiles));
        var canRunFileTreeScan = this.WhenAnyValue(x => x.IsFileTreeScanRunning)
            .Select(isRunning => !isRunning);
        OpenCreateNewFileTreeDialogCommand =
            ReactiveCommand.CreateFromTask(
                OpenCreateNewFileTreeDialogAsync,
                canRunFileTreeScan);

        RepoBrowser.RepoNodePathStringChangeCommand =
            ReactiveCommand.Create<string>(OnRepoNodePathChange);
        RepoBrowser.WhenAnyValue(x => x.RepoNodePathString)
            .InvokeCommand(RepoBrowser.RepoNodePathStringChangeCommand);
        RepoBrowser.RepoSearch.WhenAnyValue(x => x.SearchText)
            .Subscribe(_ => RefreshRepoSearch());
        RepoBrowser.RepoSearch.WhenAnyValue(x => x.CurrentMatch)
            .Where(x => x != null)
            .Select(x => x!)
            .Subscribe(NavigateToRepoSearchMatch);

        RepoBrowser.RepoNodeSelectedCommand = ReactiveCommand.Create<RepoNodeVM>(vm =>
        {
            OnSelectRepoNode(vm.RepoNode);
        });
        var repoRowSelection = RepoBrowser.RepoNodeSource.RowSelection;
        if (repoRowSelection != null)
        {
            repoRowSelection.WhenAnyValue(x => x.SelectedItem)
                .Where(x => x != null)
                .Select(x => x!)
                .Do(_ =>
                {
                    if (!_isSelectingRepoRowProgrammatically)
                        RepoBrowser.RepoSearch.DeactivateCurrentMatch();
                })
                .InvokeCommand(RepoBrowser.RepoNodeSelectedCommand);
        }

        FileBrowser.FileNodeSelectedCommand = ReactiveCommand.Create<FileNodeVM>(vm =>
        {
            OnSelectFileNode(vm.FileNode);
        });
        var fileRowSelection = FileBrowser.CurrFileNodeSource.RowSelection;
        if (fileRowSelection != null)
        {
            fileRowSelection.WhenAnyValue(x => x.SelectedItem)
                .Where(x => x != null)
                .Select(x => x!)
                .InvokeCommand(FileBrowser.FileNodeSelectedCommand);
            fileRowSelection.WhenAnyValue(x => x.SelectedItem)
                .Subscribe(x => FileBrowser.HasSelectedFileNode = x != null);
        }

        FileBrowser.DiskLabelSelectedCommand =
            ReactiveCommand.Create<string>(ChangeDiskLabel);
        FileBrowser.WhenAnyValue(x => x.SelectedDiskLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .InvokeCommand(FileBrowser.DiskLabelSelectedCommand);

        RepositoryEditor.LogNodePathCommand =
            ReactiveCommand.Create<object>(LogNodePath);
        RepositoryEditor.ExpandToSavedNodeCommand =
            ReactiveCommand.Create<object>(ExpandToSavedNode);
        RepositoryEditor.CreateChildFolderCommand =
            ReactiveCommand.Create<object>(CreateChildFolder);
        RepositoryEditor.RenameRepoNodeCommand =
            ReactiveCommand.CreateFromTask<object>(RenameRepoNodeAsync);
        RepositoryEditor.DeleteRepoNodeCommand =
            ReactiveCommand.CreateFromTask<object>(DeleteRepoNodeAsync);
        RepositoryEditor.SearchAndDeleteRepoNodesCommand =
            ReactiveCommand.CreateFromTask<object>(SearchAndDeleteRepoNodesAsync);
        RepositoryEditor.DeleteFileNodeCommand =
            ReactiveCommand.CreateFromTask<object>(DeleteFileNodeAsync);
        RepositoryEditor.JumpToCurrSelectSaveFileNodeCommand =
            ReactiveCommand.Create(JumpToCurrSelectSaveFileNode);
        RepositoryEditor.JumpToDeclareRepoNodeCommand =
            ReactiveCommand.Create<object>(JumpToDeclareRepoNode);
        RepositoryEditor.DeclareSelectedRepoNodeCommand =
            ReactiveCommand.CreateFromTask<object>(DeclareSelectedRepoNodeAsync);
        RepositoryEditor.AbandonDeclareHoldingCommand =
            ReactiveCommand.CreateFromTask<object>(AbandonDeclareHoldingAsync);
        RepositoryEditor.ChangeDeclareHoldingStrategyCommand =
            ReactiveCommand.CreateFromTask<object>(ChangeDeclareHoldingStrategyAsync);
        RepositoryEditor.OpenCurrentFileDataFolderCommand =
            ReactiveCommand.Create(OpenCurrentFileDataFolder);
        RepositoryEditor.OpenFileNodeInFolderCommand =
            ReactiveCommand.Create<object>(OpenFileNodeInFolder);
        RepositoryEditor.RefreshFileNodeFromLocalFolderCommand =
            ReactiveCommand.CreateFromTask<object>(
                RefreshFileNodeFromLocalFolderAsync,
                canRunFileTreeScan);
        RepositoryEditor.RefreshFileNodeFromLocalFolderSkippingDeclaredCommand =
            ReactiveCommand.CreateFromTask<object>(
                RefreshFileNodeFromLocalFolderSkippingDeclaredAsync,
                canRunFileTreeScan);
        RepositoryEditor.CopySelectedFileNodeToRepoNodeCommand =
            ReactiveCommand.Create<object>(
                CopySelectedFileNodeToRepoNode,
                FileBrowser.WhenAnyValue(x => x.HasSelectedFileNode));
    }

    private void ChangeDiskLabel(string diskLabel)
    {
        FileBrowser.ChangeDiskLabel(diskLabel);
    }

    private void OnRepoNodePathChange(string path)
    {
        var target = TreeNavigationService.FindRepoNodeVmByPath(
            RepoBrowser.RepoNodeVm,
            path,
            out var indexPath);
        if (target != null && indexPath != null)
        {
            var parent = indexPath.Value.Slice(0, indexPath.Value.Count - 1);
            RepoBrowser.RepoNodeSource.Expand(parent);
            SelectSingleRepoRow(indexPath.Value);
            ScrollToRepoRows();
        }
        else
        {
            RepoBrowser.RepoNodeSource.RowSelection?.Clear();
        }
    }

    private void RefreshRepoSearch(RepoNodeVM? preferredNode = null)
    {
        RepoBrowser.RepoSearch.RefreshMatches(RepoBrowser.RepoNodeVm, preferredNode);
    }

    private void NavigateToRepoSearchMatch(RepoNodeSearchMatch match)
    {
        var parent = match.IndexPath.Slice(0, match.IndexPath.Count - 1);
        RepoBrowser.RepoNodeSource.Expand(parent);
        SelectSingleRepoRow(match.IndexPath);
        ScrollToRepoRows();
    }

    private void SelectSingleRepoRow(IndexPath indexPath)
    {
        var rowSelection = RepoBrowser.RepoNodeSource.RowSelection;
        if (rowSelection == null)
            return;

        _isSelectingRepoRowProgrammatically = true;
        try
        {
            rowSelection.Clear();
            rowSelection.Select(indexPath);
        }
        finally
        {
            _isSelectingRepoRowProgrammatically = false;
        }
    }

    private void OnSelectRepoNode(RepoNode repoNode)
    {
        RepoBrowser.UpdateCurrentRepoNode(repoNode);

        if ((IsViewMode || (IsEditMode && RepositoryEditor.AutoJumpToSaveFileNode))
            && !CheckRepoNodeAndFileNodeIsSync())
        {
            JumpToCurrSelectSaveFileNode();
        }
    }

    private void OnSelectFileNode(FileNode fileNode)
    {
        if (!(IsViewMode || (IsEditMode && RepositoryEditor.AutoJumpToDeclareRepoNode))
            || CheckRepoNodeAndFileNodeIsSync())
        {
            return;
        }

        JumpToDeclareRepoNode(fileNode);
    }

    private void JumpToDeclareRepoNode(object nodeVM)
    {
        if (nodeVM is not FileNodeVM fileNodeVM)
            return;

        JumpToDeclareRepoNode(fileNodeVM.FileNode);
    }

    private void JumpToDeclareRepoNode(FileNode fileNode)
    {
        var repoNodePath = fileNode.DeclareRepoNodeDatas
            .FirstOrDefault()
            ?.RepoNodePath ?? string.Empty;
        var target = TreeNavigationService.FindRepoNodeVmByPath(
            RepoBrowser.RepoNodeVm,
            repoNodePath,
            out var indexPath);
        if (target == null || indexPath == null)
            return;

        var parent = indexPath.Value.Slice(0, indexPath.Value.Count - 1);
        RepoBrowser.RepoNodeSource.Expand(parent);
        SelectSingleRepoRow(indexPath.Value);
        ScrollToRepoRows();
    }

    public void JumpToCurrSelectSaveFileNode()
    {
        var selectRepoNode = RepoBrowser.RepoNodeSource
            .RowSelection
            ?.SelectedItem
            ?.RepoNode;
        var selectDiskLabel = RepoBrowser.SelectedSaveFileNodeLabel;
        if (selectRepoNode == null || string.IsNullOrEmpty(selectDiskLabel))
            return;

        var foundSaveData = selectRepoNode.SaveFileNodeDatas
            .Find(x => x.DiskLabel == selectDiskLabel);
        if (foundSaveData == null || !FileBrowser.ChangeDiskLabel(selectDiskLabel))
            return;

        var target = TreeNavigationService.FindFileNodeVmByPath(
            _treeProjection.GetFileNodeVm(
                FileBrowser.FileDatas[FileBrowser.CurrShowFileNodeIndex].FileNodeRoot),
            foundSaveData.FileNodePath,
            out var indexPath);
        if (target == null || indexPath == null)
            return;

        var parent = new IndexPath(indexPath.Value.Slice(0, indexPath.Value.Count - 1));
        FileBrowser.CurrFileNodeSource.Expand(parent);
        FileBrowser.CurrFileNodeSource.RowSelection?.Select(indexPath.Value);
        ScrollToFileRows();
    }

    private async Task DeclareSelectedRepoNodeAsync(object nodeVM)
    {
        if (nodeVM is not FileNodeVM fileNodeVM)
            return;

        var repoNodeVM = RepoBrowser.RepoNodeSource
            .RowSelection
            ?.SelectedItem;
        if (repoNodeVM == null)
            return;

        var diskLabel = FileBrowser.SelectedDiskLabel;
        if (string.IsNullOrWhiteSpace(diskLabel))
            return;

        var repoNode = repoNodeVM.RepoNode;
        var strategyType = repoNode.DeclareHoldingStrategyType;
        var shouldSaveStrategy = false;
        if (strategyType == null)
        {
            var selectedStrategy = await _repositoryInteraction
                .SelectInitialDeclareHoldingStrategyAsync(
                    DeclareHoldingStrategyFactory.GetAllOptions());
            if (!selectedStrategy.IsAccepted || selectedStrategy.StrategyType == null)
                return;

            strategyType = selectedStrategy.StrategyType;
            shouldSaveStrategy = true;
        }

        var declareResult = _declarationSyncService.TryDeclareHolding(
                repoNode,
                fileNodeVM.FileNode,
                diskLabel,
                strategyType.Value,
                shouldSaveStrategy);
        if (!declareResult.Succeeded)
        {
            await ShowMessageAsync(
                string.IsNullOrWhiteSpace(declareResult.FailureReason)
                    ? "声明持有失败。"
                    : declareResult.FailureReason);
            return;
        }

        ApplyChanges(declareResult.Changes);
        RepoBrowser.UpdateCurrentRepoNode(repoNode);
        MarkRepoAndFileDirty(diskLabel);
    }

    private async Task AbandonDeclareHoldingAsync(object nodeVM)
    {
        if (nodeVM is not FileNodeVM fileNodeVM)
            return;

        var repoNodePaths = fileNodeVM.FileNode.DeclareRepoNodeDatas
            .Select(x => x.RepoNodePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (repoNodePaths.Count == 0)
            return;

        var diskLabel = FileBrowser.SelectedDiskLabel;
        if (string.IsNullOrWhiteSpace(diskLabel))
            return;

        var selectedRepoNodePaths = await _repositoryInteraction
            .SelectDeclareHoldingsToAbandonAsync(repoNodePaths);
        if (selectedRepoNodePaths == null || selectedRepoNodePaths.Count == 0)
            return;

        var changes = _declarationSyncService.AbandonDeclareHoldings(
            fileNodeVM.FileNode,
            diskLabel,
            selectedRepoNodePaths);
        ApplyChanges(changes);
        MarkRepoAndFileDirty(diskLabel);

        var currentRepoNode = RepoBrowser.RepoNodeSource.RowSelection
            ?.SelectedItem
            ?.RepoNode;
        if (currentRepoNode != null)
            RepoBrowser.UpdateCurrentRepoNode(currentRepoNode);
    }

    private async Task ChangeDeclareHoldingStrategyAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var repoNode = repoNodeVM.RepoNode;
        var selectedStrategy = await _repositoryInteraction
            .SelectReplacementDeclareHoldingStrategyAsync(
                DeclareHoldingStrategyFactory.GetAllOptions(),
                repoNode.DeclareHoldingStrategyType);
        if (!selectedStrategy.IsAccepted)
            return;

        var failures = _declarationSyncService
            .GetInvalidSaveFileNodeDatasForStrategy(
                repoNode,
                selectedStrategy.StrategyType);
        if (failures.Count > 0)
        {
            var confirmed = await ShowConfirmAsync(
                BuildInvalidDeclareHoldingMessage(failures),
                "确认修改声明持有策略");
            if (!confirmed)
                return;
        }

        var changes = _declarationSyncService.ApplyDeclareHoldingStrategy(
            repoNode,
            selectedStrategy.StrategyType,
            failures);
        ApplyChanges(changes);
        RepoBrowser.UpdateCurrentRepoNode(repoNode);
        MarkRepoDirty();
        foreach (var diskLabel in failures.Select(x => x.DiskLabel).Distinct())
            MarkFileDirty(diskLabel);
    }

    private void CreateChildFolder(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var result = _repoTreeEditor.CreateChildFolder(repoNodeVM.RepoNode);
        if (!result.Succeeded || result.Value == null)
            return;

        ApplyChanges(result.Changes);
        var createdVm = _treeProjection.GetRepoNodeVm(result.Value);
        Debug.WriteLine($"CreateChildFolder: {result.Value.GetPath()}");
        RefreshRepoSearch(createdVm);
        MarkRepoAndAllFilesDirty();
    }

    private void CopySelectedFileNodeToRepoNode(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM { IsDirectory: true } repoNodeVM)
            return;

        var selectedFileNode = FileBrowser.CurrFileNodeSource
            .RowSelection
            ?.SelectedItem
            ?.FileNode;
        if (selectedFileNode == null)
            return;

        var result = _repoTreeEditor.CopyFileNodeSubtreeToRepoDirectory(
            repoNodeVM.RepoNode,
            selectedFileNode);
        if (!result.Succeeded || result.Value == null)
            return;

        ApplyChanges(result.Changes);
        var copiedVm = _treeProjection.GetRepoNodeVm(result.Value);
        RefreshRepoSearch(copiedVm);
        MarkRepoDirty();
    }

    private async Task RenameRepoNodeAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var result = await _repositoryInteraction.RequestRenameAsync(repoNodeVM.Name);
        if (string.IsNullOrWhiteSpace(result))
            return;

        var renameResult = _repoTreeEditor.RenameRepoNode(
            repoNodeVM.RepoNode,
            result);
        if (renameResult.Succeeded)
        {
            ApplyChanges(renameResult.Changes);
            RepoBrowser.RepoNodePathString = repoNodeVM.RepoNode.GetPath();
            RefreshRepoSearch(repoNodeVM);
            MarkRepoAndAllFilesDirty();
        }
    }

    private async Task DeleteRepoNodeAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var result = await _repositoryInteraction.ConfirmDeleteAsync(repoNodeVM.Name);
        if (!result)
            return;

        var deleteResult = _repoTreeEditor.DeleteRepoNode(
            repoNodeVM.RepoNode,
            RepoBrowser.RepoNodeRoot);
        if (deleteResult.Succeeded)
        {
            ApplyChanges(deleteResult.Changes);
            RefreshRepoSearch();
            MarkRepoAndAllFilesDirty();
        }
    }

    private async Task SearchAndDeleteRepoNodesAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var searchText = await _repositoryInteraction.RequestDeleteSearchAsync();
        if (string.IsNullOrWhiteSpace(searchText))
            return;

        var matchedNodes = _repoTreeEditor
            .FindDescendantRepoNodesByName(repoNodeVM.RepoNode, searchText)
            .ToList();
        if (matchedNodes.Count == 0)
        {
            await ShowMessageAsync("没有找到同名文件或目录节点。");
            return;
        }

        var confirmed = await _repositoryInteraction.ConfirmDeleteMatchesAsync(
            matchedNodes.Select(x => x.GetPath()).ToList());
        if (!confirmed)
            return;

        var deleteResult = _repoTreeEditor.DeleteRepoNodes(
            matchedNodes,
            RepoBrowser.RepoNodeRoot);
        if (deleteResult.Succeeded)
        {
            ApplyChanges(deleteResult.Changes);
            RefreshRepoSearch();
            MarkRepoAndAllFilesDirty();
        }
    }

    private async Task DeleteFileNodeAsync(object nodeVM)
    {
        if (nodeVM is not FileNodeVM fileNodeVM)
            return;

        var diskLabel = FileBrowser.SelectedDiskLabel;
        var currentFileData = FileBrowser.CurrentFileData;
        if (string.IsNullOrWhiteSpace(diskLabel)
            || currentFileData?.FileNodeRoot == null)
        {
            return;
        }

        var result = await _fileTreeInteraction.ConfirmDeleteAsync(fileNodeVM.Name);
        if (!result)
            return;

        var deleteResult = _fileTreeEditor.DeleteFileNode(
                fileNodeVM.FileNode,
                currentFileData.FileNodeRoot,
                diskLabel);
        if (deleteResult.Succeeded)
        {
            ApplyChanges(deleteResult.Changes);
            MarkRepoAndFileDirty(diskLabel);
        }
    }

    public IReadOnlyList<string> GetDirtyJsonFilePaths()
    {
        return _dirtyJsonFileTracker.GetDirtyFilePaths();
    }

    public async Task<bool> SaveDirtyFilesAsync()
    {
        var dirtyFilePaths = _dirtyJsonFileTracker.GetDirtyFilePaths();
        if (dirtyFilePaths.Count == 0)
            return true;

        try
        {
            SaveDirtyJsonFiles(dirtyFilePaths);
            _dirtyJsonFileTracker.ClearDirtyFiles(dirtyFilePaths);
            RefreshHasDirtyFiles();
            return true;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"保存 JSON 文件失败：{ex.Message}");
            return false;
        }
    }

    private void SaveDirtyJsonFiles(IReadOnlyList<string> dirtyFilePaths)
    {
        var dirtySet = new HashSet<string>(
            dirtyFilePaths,
            StringComparer.OrdinalIgnoreCase);
        var appConfigPath = _appConfigService.GetDefaultConfigPath();
        var isAppConfigDirty = dirtySet.Contains(appConfigPath);
        if (isAppConfigDirty)
            _appConfigService.SaveDefault(_appConfig);

        if (dirtySet.Contains(_treeDataStore.GetRepoFilePath(_appConfig)))
            _treeDataStore.SaveRepoRoot(_appConfig, RepoBrowser.RepoNodeRoot);

        foreach (var fileData in FileBrowser.FileDatas)
        {
            var jsonFilePath = fileData.JsonFilePath;
            if (dirtySet.Contains(jsonFilePath))
                _treeDataStore.SaveFileData(fileData);
        }

        if (isAppConfigDirty)
            _appConfig.IsDirty = false;
    }

    private void MarkRepoAndFileDirty(string diskLabel)
    {
        _dirtyJsonFileTracker.MarkRepoDirty();
        _dirtyJsonFileTracker.MarkFileDirty(diskLabel);
        RefreshHasDirtyFiles();
    }

    private void MarkRepoAndAllFilesDirty()
    {
        _dirtyJsonFileTracker.MarkRepoDirty();
        _dirtyJsonFileTracker.MarkAllFileNodesDirty();
        RefreshHasDirtyFiles();
    }

    private void MarkRepoDirty()
    {
        _dirtyJsonFileTracker.MarkRepoDirty();
        RefreshHasDirtyFiles();
    }

    private void MarkFileDirty(string diskLabel)
    {
        _dirtyJsonFileTracker.MarkFileDirty(diskLabel);
        RefreshHasDirtyFiles();
    }

    private void MarkAppConfigAndFileDirty(string diskLabel)
    {
        _appConfig.IsDirty = true;
        _dirtyJsonFileTracker.MarkAppConfigDirty();
        _dirtyJsonFileTracker.MarkFileDirty(diskLabel);
        RefreshHasDirtyFiles();
    }

    private void RefreshHasDirtyFiles()
    {
        HasDirtyFiles = _dirtyJsonFileTracker.HasDirtyFiles;
    }

    private void ApplyChanges(TreeChangeSet changes)
    {
        var selectedRepoNode = RepoBrowser.RepoNodeSource.RowSelection
            ?.SelectedItem
            ?.RepoNode;
        var selectedNodeWasRemoved = selectedRepoNode != null
                                     && changes.Changes
                                         .OfType<TreeNodeRemoved>()
                                         .Any(x => IsNodeInSubtree(
                                             selectedRepoNode,
                                             x.Node));

        _treeProjection.Apply(changes);

        if (selectedNodeWasRemoved)
        {
            RepoBrowser.RepoNodeSource.RowSelection?.Clear();
            RepoBrowser.ClearCurrentRepoNode();
        }
        else if (selectedRepoNode != null)
        {
            RepoBrowser.UpdateCurrentRepoNode(selectedRepoNode);
        }
    }

    private static bool IsNodeInSubtree(TreeNodeBase node, TreeNodeBase subtreeRoot)
    {
        TreeNodeBase? current = node;
        while (current != null)
        {
            if (ReferenceEquals(current, subtreeRoot))
                return true;
            current = current.Parent;
        }

        return false;
    }

    private bool CheckRepoNodeAndFileNodeIsSync()
    {
        var repoNode = RepoBrowser.RepoNodeSource.RowSelection
            ?.SelectedItem?.RepoNode;
        var fileNode = FileBrowser.CurrFileNodeSource.RowSelection
            ?.SelectedItem?.FileNode;

        return _declarationSyncService.CheckRepoNodeAndFileNodeIsSync(
            repoNode,
            fileNode);
    }

    private void ExpandToSavedNode(object nodeVM)
    {
        if (nodeVM is RepoNodeVM repoNodeVM)
            ExpandRepoNodeToSavedNodes(repoNodeVM);
        else if (nodeVM is FileNodeVM fileNodeVM)
            ExpandFileNodeToDeclaredNodes(fileNodeVM);
    }

    private void ExpandRepoNodeToSavedNodes(RepoNodeVM repoNodeVM)
    {
        var target = TreeNavigationService.FindRepoNodeVmByPath(
            RepoBrowser.RepoNodeVm,
            repoNodeVM.RepoNode.GetPath(),
            out var selectedIndexPath);
        if (target == null || selectedIndexPath == null)
            return;

        foreach (var relativePath in TreeNavigationService
                     .FindRepoExpandPathsToSavedNodes(repoNodeVM))
        {
            RepoBrowser.RepoNodeSource.Expand(
                AppendRelativeIndexPath(selectedIndexPath.Value, relativePath));
        }
    }

    private void ExpandFileNodeToDeclaredNodes(FileNodeVM fileNodeVM)
    {
        var rootModel = FileBrowser.FileDatas
            .ElementAtOrDefault(FileBrowser.CurrShowFileNodeIndex)
            ?.FileNodeRoot;
        var rootVm = rootModel == null
            ? null
            : _treeProjection.GetFileNodeVm(rootModel);
        if (rootVm == null)
            return;

        var target = TreeNavigationService.FindFileNodeVmByPath(
            rootVm,
            fileNodeVM.FileNode.GetPath(),
            out var selectedIndexPath);
        if (target == null || selectedIndexPath == null)
            return;

        foreach (var relativePath in TreeNavigationService
                     .FindFileExpandPathsToDeclaredNodes(fileNodeVM))
        {
            FileBrowser.CurrFileNodeSource.Expand(
                AppendRelativeIndexPath(selectedIndexPath.Value, relativePath));
        }
    }

    private static IndexPath AppendRelativeIndexPath(
        IndexPath basePath,
        IndexPath relativePath)
    {
        var indexes = new List<int>();
        for (var i = 0; i < basePath.Count; i++)
            indexes.Add(basePath[i]);
        for (var i = 1; i < relativePath.Count; i++)
            indexes.Add(relativePath[i]);

        return new IndexPath(indexes);
    }

    private void LogNodePath(object nodeVM)
    {
        if (nodeVM is RepoNodeVM repoNodeVM)
        {
            var path = repoNodeVM.RepoNode.GetPath();
            Console.WriteLine($"仓库节点路径: {path}");
            Debug.WriteLine($"仓库节点路径: {path}");

            var saveDatas = repoNodeVM.RepoNode.SaveFileNodeDatas;
            Console.WriteLine($"  SaveFileNodeDatas 数量: {saveDatas.Count}");
            Debug.WriteLine($"  SaveFileNodeDatas 数量: {saveDatas.Count}");

            foreach (var data in saveDatas)
            {
                Console.WriteLine($"    - DiskLabel: {data.DiskLabel}, FileNodePath: {data.FileNodePath}");
                Debug.WriteLine($"    - DiskLabel: {data.DiskLabel}, FileNodePath: {data.FileNodePath}");
            }
        }
        else if (nodeVM is FileNodeVM fileNodeVM)
        {
            var path = fileNodeVM.FileNode.GetPath();
            Console.WriteLine($"文件节点路径: {path}");
            Debug.WriteLine($"文件节点路径: {path}");

            var declareDatas = fileNodeVM.FileNode.DeclareRepoNodeDatas;
            Console.WriteLine($"  DeclareRepoNodeDatas 数量: {declareDatas.Count}");
            Debug.WriteLine($"  DeclareRepoNodeDatas 数量: {declareDatas.Count}");

            foreach (var data in declareDatas)
            {
                Console.WriteLine($"    - RepoNodePath: {data.RepoNodePath}");
                Debug.WriteLine($"    - RepoNodePath: {data.RepoNodePath}");
            }
        }
        else
        {
            Console.WriteLine("未知节点类型");
            Debug.WriteLine("未知节点类型");
        }
    }

    private void OpenCurrentFileDataFolder()
    {
        var localFolderPath = FileBrowser.CurrentFileData?.LocalFolderPath;
        if (string.IsNullOrWhiteSpace(localFolderPath))
            return;

        _pathOpener.OpenFolder(localFolderPath);
    }

    private void OpenFileNodeInFolder(object nodeVM)
    {
        if (nodeVM is not FileNodeVM fileNodeVM)
            return;

        var localPath = GetLocalPath(fileNodeVM.FileNode);
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        if (fileNodeVM.FileNode.Parent == null)
            _pathOpener.OpenFolder(localPath);
        else
            _pathOpener.ShowPathInFolder(localPath);
    }

    private async Task RefreshFileNodeFromLocalFolderAsync(object nodeVM)
    {
        await RefreshFileNodeFromLocalFolderAsync(
            nodeVM,
            skipDeclaredSubtrees: false);
    }

    private async Task RefreshFileNodeFromLocalFolderSkippingDeclaredAsync(object nodeVM)
    {
        await RefreshFileNodeFromLocalFolderAsync(
            nodeVM,
            skipDeclaredSubtrees: true);
    }

    private async Task RefreshFileNodeFromLocalFolderAsync(
        object nodeVM,
        bool skipDeclaredSubtrees)
    {
        if (nodeVM is not FileNodeVM { IsDirectory: true } fileNodeVM)
            return;

        var diskLabel = FileBrowser.SelectedDiskLabel;
        if (string.IsNullOrWhiteSpace(diskLabel))
            return;

        var localPath = GetLocalPath(fileNodeVM.FileNode);
        if (string.IsNullOrWhiteSpace(localPath))
        {
            await ShowMessageAsync("没有配置对应的本地文件夹，无法刷新。");
            return;
        }

        var scanResult = await RunFileTreeScanAsync((progress, cancellationToken) =>
        {
            var fileTreeScan = _fileTreeScanner.Scan(
                new FileTreeScanRequest(
                    localPath,
                    fileNodeVM.FileNode,
                    skipDeclaredSubtrees),
                progress,
                cancellationToken);
            if (fileTreeScan.Status != FileTreeScanStatus.Succeeded
                || fileTreeScan.Root == null)
            {
                return new RefreshFileNodeScanResult(
                    fileTreeScan,
                    RefreshedFileNode: null,
                    Array.Empty<DeclareHoldingValidationFailure>());
            }

            var refreshedFileNode = _declarationSyncService.BuildRefreshedFileNodeSubtree(
                fileNodeVM.FileNode,
                fileTreeScan.Root);
            var failures = _declarationSyncService
                .GetInvalidDeclareHoldingsAfterRefresh(
                    diskLabel,
                    fileNodeVM.FileNode,
                    refreshedFileNode);

            return new RefreshFileNodeScanResult(
                fileTreeScan,
                refreshedFileNode,
                failures);
        });
        if (scanResult.IsCancelled)
            return;
        if (scanResult.Error != null)
        {
            await ShowMessageAsync($"读取本地文件夹失败：{scanResult.Error.Message}");
            return;
        }
        if (scanResult.Value?.FileTreeScan.Status == FileTreeScanStatus.Cancelled)
            return;
        if (scanResult.Value == null
            || scanResult.Value.FileTreeScan.Status != FileTreeScanStatus.Succeeded
            || scanResult.Value.RefreshedFileNode == null)
        {
            await ShowFileTreeScanIssuesAsync(
                "读取本地文件夹失败，文件树未刷新。",
                scanResult.Value?.FileTreeScan.BlockingIssues
                ?? Array.Empty<FileTreeScanIssue>());
            return;
        }

        var refreshedFileNode = scanResult.Value.RefreshedFileNode;
        var failures = scanResult.Value.Failures;

        if (failures.Count > 0)
        {
            var confirmed = await ShowConfirmAsync(
                BuildRefreshInvalidDeclareHoldingMessage(failures),
                "确认刷新文件树");
            if (!confirmed)
                return;
        }

        var changes = _declarationSyncService.ApplyFileNodeRefresh(
            diskLabel,
            fileNodeVM.FileNode,
            refreshedFileNode,
            failures);
        ApplyChanges(changes);

        var currentRepoNode = RepoBrowser.RepoNodeSource.RowSelection
            ?.SelectedItem
            ?.RepoNode;
        if (currentRepoNode != null)
            RepoBrowser.UpdateCurrentRepoNode(currentRepoNode);

        if (failures.Count > 0)
            MarkRepoAndFileDirty(diskLabel);
        else
            MarkFileDirty(diskLabel);

        if (scanResult.Value.FileTreeScan.Warnings.Count > 0)
        {
            await ShowFileTreeScanIssuesAsync(
                "文件树已刷新，但扫描过程中出现以下警告：",
                scanResult.Value.FileTreeScan.Warnings);
        }
    }

    private string? GetLocalPath(FileNode fileNode)
    {
        var localFolderPath = FileBrowser.CurrentFileData?.LocalFolderPath;
        if (string.IsNullOrWhiteSpace(localFolderPath))
            return null;

        var pathSegments = fileNode.GetPath()
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .ToArray();
        return pathSegments.Aggregate(localFolderPath, Path.Combine);
    }

    public async void OpenCreateNewFileTreeDialog()
    {
        await OpenCreateNewFileTreeDialogAsync();
    }

    private async Task OpenCreateNewFileTreeDialogAsync()
    {
        Debug.WriteLine("OpenCreateNewFileTreeDialog");
        var result = await _fileTreeInteraction.RequestNewFileTreeAsync();
        if (result == null)
            return;

        var selectedPath = result.Path.Trim();
        var tag = result.DiskLabel.Trim();
        if (string.IsNullOrWhiteSpace(selectedPath)
            || string.IsNullOrWhiteSpace(tag))
        {
            await ShowMessageAsync("请选择文件夹并填写标签。");
            return;
        }

        if (tag.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await ShowMessageAsync("标签包含不能用于文件名的字符，请修改标签。");
            return;
        }

        var relativeJsonFilePath = $"{tag}.json";
        var jsonFilePath = Path.Combine(_appConfig.JsonFilePath, relativeJsonFilePath);
        if (FileBrowser.FileDatas.Any(x =>
                string.Equals(x.DiskLabel, tag, StringComparison.OrdinalIgnoreCase)))
        {
            await ShowMessageAsync($"已存在标签为 {tag} 的文件树，请修改标签。");
            return;
        }

        if (File.Exists(jsonFilePath))
        {
            await ShowMessageAsync($"文件树 JSON 已存在：{jsonFilePath}");
            return;
        }

        Console.WriteLine($"选中的文件夹: {selectedPath}");
        Console.WriteLine($"填写的标签: {tag}");

        var scanResult = await RunFileTreeScanAsync((progress, cancellationToken) =>
            _fileTreeScanner.Scan(
                new FileTreeScanRequest(selectedPath),
                progress,
                cancellationToken));
        if (scanResult.IsCancelled)
            return;
        if (scanResult.Error != null)
        {
            await ShowMessageAsync($"读取本地文件夹失败：{scanResult.Error.Message}");
            return;
        }
        if (scanResult.Value?.Status == FileTreeScanStatus.Cancelled)
            return;
        if (scanResult.Value?.Status != FileTreeScanStatus.Succeeded
            || scanResult.Value.Root == null)
        {
            await ShowFileTreeScanIssuesAsync(
                "读取本地文件夹失败，未创建文件树。",
                scanResult.Value?.BlockingIssues
                ?? Array.Empty<FileTreeScanIssue>());
            return;
        }

        var fileData = new FileData
        {
            DiskLabel = tag,
            LocalFolderPath = selectedPath,
            JsonFilePath = jsonFilePath,
            FileNodeRoot = scanResult.Value.Root
        };
        EnsureAppConfigFileDataFilesInitialized();
        FileBrowser.AddFileData(fileData);
        _appConfig.FileDataFiles.Add(new FileDataFileConfig
        {
            JsonFilePath = relativeJsonFilePath,
            LocalFolderPath = selectedPath
        });
        _dirtyJsonFileTracker.SetFileNodePath(tag, jsonFilePath);
        MarkAppConfigAndFileDirty(tag);

        if (scanResult.Value.Warnings.Count > 0)
        {
            await ShowFileTreeScanIssuesAsync(
                "文件树已创建，但扫描过程中出现以下警告：",
                scanResult.Value.Warnings);
        }
    }

    private void EnsureAppConfigFileDataFilesInitialized()
    {
        if (_appConfig.FileDataFiles.Count > 0)
            return;

        foreach (var fileData in FileBrowser.FileDatas)
        {
            var jsonFilePath = fileData.JsonFilePath;
            if (string.IsNullOrWhiteSpace(jsonFilePath))
                continue;

            _appConfig.FileDataFiles.Add(new FileDataFileConfig
            {
                JsonFilePath = Path.GetRelativePath(_appConfig.JsonFilePath, jsonFilePath),
                LocalFolderPath = fileData.LocalFolderPath
            });
        }
    }

    public void ShowRepoNode()
    {
        var select = RepoBrowser.RepoNodeSource.RowSelection?.SelectedItem;
        Console.WriteLine(select?.Name ?? "");
    }

    private async Task<FileTreeScanExecutionResult<T>> RunFileTreeScanAsync<T>(
        Func<IProgress<FileTreeScanProgress>, CancellationToken, T> scan)
    {
        IsFileTreeScanRunning = true;
        try
        {
            return await _fileTreeScanProgressRunner.RunAsync(scan);
        }
        finally
        {
            IsFileTreeScanRunning = false;
        }
    }

    private Task ShowMessageAsync(string message)
    {
        return _userInteraction.ShowMessageAsync(new MessageRequest(message));
    }

    private async Task ShowFileTreeScanIssuesAsync(
        string summary,
        IReadOnlyList<FileTreeScanIssue> issues)
    {
        const int maxDisplayedIssues = 20;
        var builder = new StringBuilder();
        builder.AppendLine(summary);

        if (issues.Count == 0)
        {
            builder.AppendLine("没有可用的错误详情。");
        }
        else
        {
            builder.AppendLine();
            foreach (var issue in issues.Take(maxDisplayedIssues))
            {
                builder.AppendLine($"- {issue.Path}");
                builder.AppendLine($"  {issue.Message}");
            }

            var remainingCount = issues.Count - maxDisplayedIssues;
            if (remainingCount > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"另有 {remainingCount} 个问题未展示。");
            }
        }

        await _userInteraction.ShowMessageAsync(
            new MessageRequest(builder.ToString(), MessageDisplayKind.Detailed));
    }

    private Task<bool> ShowConfirmAsync(
        string message,
        string title = "确认")
    {
        return _userInteraction.ConfirmAsync(
            new ConfirmationRequest(message, title));
    }

    private static string BuildInvalidDeclareHoldingMessage(
        IReadOnlyList<DeclareHoldingValidationFailure> failures)
    {
        var builder = new StringBuilder();
        builder.AppendLine("以下 FileNode 不满足新的声明持有策略。");
        builder.AppendLine("点击确定会删除这些不通过的声明持有关系，点击取消则不做修改。");
        builder.AppendLine();

        foreach (var failure in failures)
        {
            builder.AppendLine(
                $"- [{failure.DiskLabel}] {failure.FileNodePath}");
            if (!string.IsNullOrWhiteSpace(failure.FailureReason))
                builder.AppendLine($"  {failure.FailureReason}");
        }

        return builder.ToString();
    }

    private static string BuildRefreshInvalidDeclareHoldingMessage(
        IReadOnlyList<DeclareHoldingValidationFailure> failures)
    {
        var builder = new StringBuilder();
        builder.AppendLine("刷新后以下声明持有关系将不再成立。");
        builder.AppendLine("点击确定会应用刷新并删除这些不通过的声明持有关系，点击取消则不做修改。");
        builder.AppendLine();

        foreach (var failure in failures)
        {
            builder.AppendLine(
                $"- [{failure.DiskLabel}] {failure.FileNodePath}");
            if (!string.IsNullOrWhiteSpace(failure.RepoNodePath))
                builder.AppendLine($"  Repo: {failure.RepoNodePath}");
            if (!string.IsNullOrWhiteSpace(failure.FailureReason))
                builder.AppendLine($"  {failure.FailureReason}");
        }

        return builder.ToString();
    }

    private static void ScrollToRepoRows()
    {
        MessageBus.Current.SendMessage(
            new TargetTreeRowMessage(TreeControlNames.ViewRepoTree));
        MessageBus.Current.SendMessage(
            new TargetTreeRowMessage(TreeControlNames.EditRepoTree));
    }

    private static void ScrollToFileRows()
    {
        MessageBus.Current.SendMessage(
            new TargetTreeRowMessage(TreeControlNames.ViewFileTree));
        MessageBus.Current.SendMessage(
            new TargetTreeRowMessage(TreeControlNames.EditFileTree));
    }

    private sealed record RefreshFileNodeScanResult(
        FileTreeScanResult FileTreeScan,
        FileNode? RefreshedFileNode,
        IReadOnlyList<DeclareHoldingValidationFailure> Failures);
}
