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
    private readonly DeclarationSyncService _declarationSyncService;
    private readonly RepoTreeEditor _repoTreeEditor;
    private readonly AppConfig _appConfig;

    public RepoBrowserViewModel RepoBrowser { get; }
    public FileBrowserViewModel FileBrowser { get; }
    public RepositoryEditorViewModel RepositoryEditor { get; }

    [Reactive] public int ViewModeTabIndex { get; set; }

    public bool IsViewMode => ViewModeTabIndex == 0;
    public bool IsEditMode => ViewModeTabIndex == 1;

    public MainWindowViewModel()
    {
        _appConfig = _appConfigService.LoadDefault();

        var repoNodeRoot = _treeDataStore.LoadRepoRoot(_appConfig);
        RepoBrowser = new RepoBrowserViewModel(repoNodeRoot);
        FileBrowser = new FileBrowserViewModel(
            _treeDataStore.LoadFileDataVmBundles(_appConfig));
        RepositoryEditor = new RepositoryEditorViewModel();

        _declarationSyncService = new DeclarationSyncService(
            RepoBrowser.RepoNodeRoot,
            RepoBrowser.RepoNodeVm,
            FileBrowser.FileDataVmBundles);
        _repoTreeEditor = new RepoTreeEditor(_declarationSyncService);

        InitCommand();
    }

    private void InitCommand()
    {
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
    }

    private void CreateChildFolder(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        var createdVm = _repoTreeEditor.CreateChildFolder(repoNodeVM);
        Debug.WriteLine($"CreateChildFolder: {createdVm.RepoNode.GetPath()}");
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
            RepoBrowser.RepoNodePathString = repoNodeVM.RepoNode.GetPath();
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

        _repoTreeEditor.DeleteRepoNode(
            repoNodeVM,
            RepoBrowser.RepoNodeRoot,
            RepoBrowser.RepoNodeVm);
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

        var result = await dialog.ShowDialog<(string? path, string? tag)?>(GetMainWindow());
        if (result is not { path: not null, tag: not null })
            return;

        Console.WriteLine($"选中的文件夹: {result?.path}");
        Console.WriteLine($"填写的标签: {result?.tag}");

        var bundle = _treeDataStore.CreateFileDataVmBundleFromPath(
            result?.tag ?? string.Empty,
            result?.path ?? string.Empty);
        FileBrowser.AddBundle(bundle);
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
