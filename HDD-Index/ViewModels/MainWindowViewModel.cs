using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DynamicData.Kernel;
using HDD_Index.Messages;
using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AppConfigService _appConfigService = new();
    private readonly TreeDataStore _treeDataStore = new();
    private readonly DirtyJsonFileTracker _dirtyJsonFileTracker = new();
    private readonly DeclarationSyncService _declarationSyncService;
    private readonly RepoTreeEditor _repoTreeEditor;
    private readonly AppConfig _appConfig;

    public RepoBrowserViewModel RepoBrowser { get; }
    public FileBrowserViewModel FileBrowser { get; }
    public RepositoryEditorViewModel RepositoryEditor { get; }

    [Reactive] public int ViewModeTabIndex { get; set; }
    [Reactive] public bool HasDirtyFiles { get; set; }

    public bool IsViewMode => ViewModeTabIndex == 0;
    public bool IsEditMode => ViewModeTabIndex == 1;
    public ReactiveCommand<Unit, Unit> SaveCommand { get; private set; } = null!;

    public MainWindowViewModel()
    {
        _appConfig = _appConfigService.LoadDefault();

        var repoNodeRoot = _treeDataStore.LoadRepoRoot(_appConfig);
        RepoBrowser = new RepoBrowserViewModel(repoNodeRoot);
        var fileDataVmBundles = _treeDataStore.LoadFileDataVmBundles(_appConfig);
        FileBrowser = new FileBrowserViewModel(fileDataVmBundles);
        RepositoryEditor = new RepositoryEditorViewModel();
        InitDirtyTracker();

        _declarationSyncService = new DeclarationSyncService(
            RepoBrowser.RepoNodeRoot,
            RepoBrowser.RepoNodeVm,
            FileBrowser.FileDataVmBundles);
        _repoTreeEditor = new RepoTreeEditor(_declarationSyncService);

        InitCommand();
    }

    private void InitDirtyTracker()
    {
        _dirtyJsonFileTracker.SetAppConfigPath(_appConfigService.GetDefaultConfigPath());
        _dirtyJsonFileTracker.SetRepoFilePath(_treeDataStore.GetRepoFilePath(_appConfig));
        foreach (var bundle in FileBrowser.FileDataVmBundles)
        {
            _dirtyJsonFileTracker.SetFileNodePath(
                bundle.FileData.DiskLabel,
                bundle.FileData.JsonFilePath);
        }
    }

    private void InitCommand()
    {
        SaveCommand = ReactiveCommand.CreateFromTask(
            async () => { await SaveDirtyFilesAsync(); },
            this.WhenAnyValue(x => x.HasDirtyFiles));

        RepoBrowser.RepoNodePathStringChangeCommand =
            ReactiveCommand.Create<string>(OnRepoNodePathChange);
        RepoBrowser.WhenAnyValue(x => x.RepoNodePathString)
            .InvokeCommand(RepoBrowser.RepoNodePathStringChangeCommand);

        RepoBrowser.RepoNodeSelectedCommand = ReactiveCommand.Create<RepoNodeVM>(vm =>
        {
            OnSelectRepoNode(vm.RepoNode);
        });
        RepoBrowser.WhenAnyValue(x => x.RepoNodeSource.RowSelection.SelectedItem)
            .Where(x => x != null)
            .Select(x => x!)
            .InvokeCommand(RepoBrowser.RepoNodeSelectedCommand);

        FileBrowser.FileNodeSelectedCommand = ReactiveCommand.Create<FileNodeVM>(vm =>
        {
            OnSelectFileNode(vm.FileNode);
        });
        FileBrowser.WhenAnyValue(x => x.CurrFileNodeSource.RowSelection.SelectedItem)
            .Where(x => x != null)
            .Select(x => x!)
            .InvokeCommand(FileBrowser.FileNodeSelectedCommand);

        FileBrowser.DiskLabelSelectedCommand =
            ReactiveCommand.Create<string>(ChangeDiskLabel);
        FileBrowser.WhenAnyValue(x => x.SelectedDiskLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .InvokeCommand(FileBrowser.DiskLabelSelectedCommand);

        RepositoryEditor.LogNodePathCommand =
            ReactiveCommand.Create<object>(LogNodePath);
        RepositoryEditor.CreateChildFolderCommand =
            ReactiveCommand.Create<object>(CreateChildFolder);
        RepositoryEditor.RenameRepoNodeCommand =
            ReactiveCommand.CreateFromTask<object>(RenameRepoNodeAsync);
        RepositoryEditor.DeleteRepoNodeCommand =
            ReactiveCommand.CreateFromTask<object>(DeleteRepoNodeAsync);
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
            RepoBrowser.RepoNodeSource.RowSelection?.Select(indexPath.Value);
            ScrollToRepoRows();
        }
        else
        {
            RepoBrowser.RepoNodeSource.RowSelection?.Clear();
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
        RepoBrowser.RepoNodeSource.RowSelection?.Select(indexPath.Value);
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
            FileBrowser.FileDataVmBundles[FileBrowser.CurrShowFileNodeIndex].FileNodeVm,
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
            var owner = GetMainWindow();
            if (owner == null)
                return;

            var dialog = new StrategySelectionDialog(
                DeclareHoldingStrategyFactory.GetAllOptions())
            {
                Title = "选择声明持有策略",
                Width = 420,
                Height = 260,
            };

            var selectedStrategy =
                await dialog.ShowDialog<StrategySelectionDialogResult?>(owner);
            if (selectedStrategy == null || selectedStrategy.StrategyType == null)
                return;

            strategyType = selectedStrategy.StrategyType;
            shouldSaveStrategy = true;
        }

        if (!_declarationSyncService.TryDeclareHolding(
                repoNode,
                repoNodeVM,
                fileNodeVM.FileNode,
                fileNodeVM,
                diskLabel,
                strategyType.Value,
                shouldSaveStrategy,
                out var failureReason))
        {
            await ShowMessageAsync(
                string.IsNullOrWhiteSpace(failureReason)
                    ? "声明持有失败。"
                    : failureReason);
            return;
        }

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

        var owner = GetMainWindow();
        if (owner == null)
            return;

        var dialog = new AbandonDeclareHoldingDialog(repoNodePaths)
        {
            Title = "放弃声明持有",
            Width = 520,
            Height = 320,
        };

        var selectedRepoNodePaths =
            await dialog.ShowDialog<List<string>?>(owner);
        if (selectedRepoNodePaths == null || selectedRepoNodePaths.Count == 0)
            return;

        _declarationSyncService.AbandonDeclareHoldings(
            fileNodeVM.FileNode,
            diskLabel,
            selectedRepoNodePaths);
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

        var owner = GetMainWindow();
        if (owner == null)
            return;

        var repoNode = repoNodeVM.RepoNode;
        var dialog = new StrategySelectionDialog(
            DeclareHoldingStrategyFactory.GetAllOptions(),
            includeClearOption: true,
            selectedStrategyType: repoNode.DeclareHoldingStrategyType)
        {
            Title = "修改声明持有的策略",
            Width = 420,
            Height = 260,
        };

        var selectedStrategy =
            await dialog.ShowDialog<StrategySelectionDialogResult?>(owner);
        if (selectedStrategy == null)
            return;

        var failures = _declarationSyncService
            .GetInvalidSaveFileNodeDatasForStrategy(
                repoNode,
                selectedStrategy.StrategyType);
        if (failures.Count > 0)
        {
            var confirmed = await ShowConfirmAsync(
                BuildInvalidDeclareHoldingMessage(failures));
            if (!confirmed)
                return;
        }

        _declarationSyncService.ApplyDeclareHoldingStrategy(
            repoNode,
            selectedStrategy.StrategyType,
            failures);
        RepoBrowser.UpdateCurrentRepoNode(repoNode);
        MarkRepoDirty();
        foreach (var diskLabel in failures.Select(x => x.DiskLabel).Distinct())
            MarkFileDirty(diskLabel);
    }

    private void CreateChildFolder(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var createdVm = _repoTreeEditor.CreateChildFolder(repoNodeVM);
        Debug.WriteLine($"CreateChildFolder: {createdVm.RepoNode.GetPath()}");
        MarkRepoAndAllFilesDirty();
    }

    private async Task RenameRepoNodeAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var dialog = new RenameRepoNodeDialog(repoNodeVM.Name)
        {
            Title = "重命名",
            Width = 420,
            Height = 160,
        };

        var result = await dialog.ShowDialog<string?>(GetMainWindow());
        if (string.IsNullOrWhiteSpace(result))
            return;

        if (_repoTreeEditor.RenameRepoNode(repoNodeVM, result))
        {
            RepoBrowser.RepoNodePathString = repoNodeVM.RepoNode.GetPath();
            MarkRepoAndAllFilesDirty();
        }
    }

    private async Task DeleteRepoNodeAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var dialog = new DeleteConfirmDialog(repoNodeVM.Name)
        {
            Title = "确认删除",
            Width = 400,
            Height = 150,
        };

        var result = await dialog.ShowDialog<bool>(GetMainWindow());
        if (!result)
            return;

        if (_repoTreeEditor.DeleteRepoNode(
            repoNodeVM,
            RepoBrowser.RepoNodeRoot,
            RepoBrowser.RepoNodeVm))
        {
            MarkRepoAndAllFilesDirty();
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

        foreach (var bundle in FileBrowser.FileDataVmBundles)
        {
            var jsonFilePath = bundle.FileData.JsonFilePath;
            if (dirtySet.Contains(jsonFilePath))
                _treeDataStore.SaveFileDataBundle(bundle);
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

        OpenFolderInExplorer(localFolderPath);
    }

    private void OpenFileNodeInFolder(object nodeVM)
    {
        if (nodeVM is not FileNodeVM fileNodeVM)
            return;

        var localPath = GetLocalPath(fileNodeVM.FileNode);
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        OpenPathInExplorer(localPath, fileNodeVM.FileNode.Parent == null);
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

    private static void OpenFolderInExplorer(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.WriteLine($"本地文件夹不存在: {folderPath}");
            Console.WriteLine($"本地文件夹不存在: {folderPath}");
            return;
        }

        StartExplorer($"\"{folderPath}\"");
    }

    private static void OpenPathInExplorer(string path, bool openFolderDirectly)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            Debug.WriteLine($"本地路径不存在: {path}");
            Console.WriteLine($"本地路径不存在: {path}");
            return;
        }

        StartExplorer(openFolderDirectly ? $"\"{path}\"" : $"/select,\"{path}\"");
    }

    private static void StartExplorer(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true
        });
    }

    public async void OpenCreateNewFileTreeDialog()
    {
        Debug.WriteLine("OpenCreateNewFileTreeDialog");
        var dialog = new FolderSelectDialog
        {
            Title = "选择文件夹并填写标签",
            Width = 450,
            Height = 150,
        };

        var owner = GetMainWindow();
        if (owner == null)
            return;

        var result = await dialog.ShowDialog<(string? path, string? tag)?>(owner);
        if (result is not { path: not null, tag: not null })
            return;

        var selectedPath = result.Value.path.Trim();
        var tag = result.Value.tag.Trim();
        if (string.IsNullOrWhiteSpace(selectedPath)
            || string.IsNullOrWhiteSpace(tag))
        {
            await ShowMessageAsync("请选择文件夹并填写标签。");
            return;
        }

        if (!Directory.Exists(selectedPath))
        {
            await ShowMessageAsync($"选择的文件夹不存在：{selectedPath}");
            return;
        }

        if (tag.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await ShowMessageAsync("标签包含不能用于文件名的字符，请修改标签。");
            return;
        }

        var relativeJsonFilePath = $"{tag}.json";
        var jsonFilePath = Path.Combine(_appConfig.JsonFilePath, relativeJsonFilePath);
        if (FileBrowser.FileDataVmBundles.Any(x =>
                string.Equals(x.FileData.DiskLabel, tag, StringComparison.OrdinalIgnoreCase)))
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

        var bundle = _treeDataStore.CreateFileDataVmBundleFromPath(
            tag,
            selectedPath,
            jsonFilePath);
        EnsureAppConfigFileDataFilesInitialized();
        FileBrowser.AddBundle(bundle);
        _appConfig.FileDataFiles.Add(new FileDataFileConfig
        {
            JsonFilePath = relativeJsonFilePath,
            LocalFolderPath = selectedPath
        });
        _dirtyJsonFileTracker.SetFileNodePath(tag, jsonFilePath);
        MarkAppConfigAndFileDirty(tag);
    }

    private void EnsureAppConfigFileDataFilesInitialized()
    {
        if (_appConfig.FileDataFiles.Count > 0)
            return;

        foreach (var bundle in FileBrowser.FileDataVmBundles)
        {
            var jsonFilePath = bundle.FileData.JsonFilePath;
            if (string.IsNullOrWhiteSpace(jsonFilePath))
                continue;

            _appConfig.FileDataFiles.Add(new FileDataFileConfig
            {
                JsonFilePath = Path.GetRelativePath(_appConfig.JsonFilePath, jsonFilePath),
                LocalFolderPath = bundle.FileData.LocalFolderPath
            });
        }
    }

    public void ShowRepoNode()
    {
        var select = RepoBrowser.RepoNodeSource.RowSelection?.SelectedItem;
        Console.WriteLine(select?.Name ?? "");
    }

    private static Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
    }

    private static async Task ShowMessageAsync(string message)
    {
        var owner = GetMainWindow();
        if (owner == null)
            return;

        var dialog = new MessageDialog(message)
        {
            Title = "提示",
            Width = 400,
            Height = 150,
        };
        await dialog.ShowDialog(owner);
    }

    private static async Task<bool> ShowConfirmAsync(string message)
    {
        var owner = GetMainWindow();
        if (owner == null)
            return false;

        var dialog = new ConfirmMessageDialog(message)
        {
            Title = "确认修改声明持有策略",
            Width = 520,
            Height = 260,
        };
        return await dialog.ShowDialog<bool>(owner);
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

    private static void ScrollToRepoRows()
    {
        MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.ViewRepoTree));
        MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.EditRepoTree));
    }

    private static void ScrollToFileRows()
    {
        MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.ViewFileTree));
        MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.EditFileTree));
    }
}
