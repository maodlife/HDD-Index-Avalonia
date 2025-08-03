using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using DynamicData;
using DynamicData.Kernel;
using HDD_Index.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private static string folderPath =
        "/Users/maodlife/Documents/HDD-Index/config/";

    private static string repoFileName = "RepoTreeData.txt";

    #region Repo Data

    public RepoNode RepoNodeRoot { get; set; }
    public RepoNodeVM RepoNodeVm { get; set; }

    [Reactive]
    public HierarchicalTreeDataGridSource<RepoNodeVM> RepoNodeSource
    {
        get;
        set;
    }

    public ReactiveCommand<RepoNodeVM, Unit> RepoNodeSelectedCommand
    {
        get;
        set;
    }

    /// <summary>
    /// combobox中要显示的保存了当前repo节点的磁盘名
    /// </summary>
    public ObservableCollection<string>
        CurrRepoNodeSaveFileNodes { get; set; } =
        new ObservableCollection<string>();

    [Reactive] public string SelectedSaveFileNodeLabel { get; set; }

    [Reactive] public bool AutoJumpToSaveFileNode { get; set; } = false;

    #endregion

    #region File Data

    public List<FileDataVMBundle> FileDataVmBundles { get; set; } =
        new List<FileDataVMBundle>();

    public int CurrShowFileNodeIndex { get; set; } = 0;

    public ObservableCollection<FileNodeVM> FileNodeVm { get; set; }
        = new ObservableCollection<FileNodeVM>();

    /// <summary>
    /// 当前实际用于View动态绑定的source
    /// </summary>
    [Reactive]
    public HierarchicalTreeDataGridSource<FileNodeVM> CurrFileNodeSource
    {
        get;
        set;
    }
    
    public ReactiveCommand<FileNodeVM, Unit> FileNodeSelectedCommand
    {
        get;
        set;
    }

    [Reactive]
    public ObservableCollection<string> DiskLabels { get; set; } =
        new ObservableCollection<string>();

    [Reactive] public string SelectedDiskLabel { get; set; }

    public ReactiveCommand<string, Unit> DiskLabelSelectedCommand { get; set; }

    [Reactive] public bool AutoJumpToDeclareRepoNode { get; set; } = false;
    
    #endregion File Data

    #region View Mode Tab

    // 选择了浏览还是编辑
    [Reactive] public int ViewModeTabIndex { get; set; } = 0;

    public bool IsViewMode => ViewModeTabIndex == 0;
    public bool IsEditMode => ViewModeTabIndex == 1;

    #endregion

    #region 初始化

    public MainWindowViewModel()
    {
        InitRepoData();
        InitFileData();
        InitCommand();
    }

    private void InitCommand()
    {
        RepoNodeSelectedCommand = ReactiveCommand.Create<RepoNodeVM>(vm =>
        {
            SelectRepoNode(vm.RepoNode);
        });

        this.WhenAnyValue(x =>
                x.RepoNodeSource.RowSelection.SelectedItem)
            .Where(x => x != null)
            .InvokeCommand(RepoNodeSelectedCommand);
        
        FileNodeSelectedCommand = ReactiveCommand.Create<FileNodeVM>(vm =>
        {
            SelectFileNode(vm.FileNode);
        });

        this.WhenAnyValue(x =>
                x.CurrFileNodeSource.RowSelection.SelectedItem)
            .Where(x => x != null)
            .InvokeCommand(FileNodeSelectedCommand);

        DiskLabelSelectedCommand =
            ReactiveCommand.Create<string>(ChangeDiskLabel);

        this.WhenAnyValue(x => x.SelectedDiskLabel)
            .Where(x => x != null)
            .InvokeCommand(DiskLabelSelectedCommand);
    }

    private void InitRepoData()
    {
        var repoNodeFilePath = Path.Combine(folderPath, repoFileName);
        var json = File.ReadAllText(repoNodeFilePath);
        RepoNodeRoot = JsonSerializer.Deserialize<RepoNode>(json);

        RepoNodeVm = RepoNodeVM.Create(RepoNodeRoot);

        RepoNodeSource =
            new HierarchicalTreeDataGridSource<RepoNodeVM>(RepoNodeVm)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<RepoNodeVM>(
                        new TextColumn<RepoNodeVM, string>(
                            "Name",
                            x => x.Name),
                        x => x.Children),
                    new TextColumn<RepoNodeVM, string>(
                        "存储数",
                        x => x.SaveFileNodeCntString)
                }
            };
    }

    private void InitFileData()
    {
        var files = Directory.GetFiles(folderPath);
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == repoFileName)
                continue;
            var json = File.ReadAllText(file);
            var bundle =
                FileDataVMBundle.Create(
                    Path.GetFileNameWithoutExtension(file),
                    json);
            FileDataVmBundles.Add(bundle);
        }

        FileDataVmBundles.Sort((lhs, rhs)
            => String.Compare(
                lhs.FileData.DiskLabel,
                rhs.FileData.DiskLabel,
                StringComparison.Ordinal));

        foreach (var item in FileDataVmBundles)
        {
            DiskLabels.Add(item.FileData.DiskLabel);
        }

        // 默认显示第一个
        if (FileDataVmBundles.Count > 0)
        {
            ChangeFileNodeVM(FileDataVmBundles[0].FileNodeVm);
            SelectedDiskLabel = DiskLabels[0];
        }

        CurrFileNodeSource =
            new HierarchicalTreeDataGridSource<FileNodeVM>(FileNodeVm)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<FileNodeVM>(
                        new TemplateColumn<FileNodeVM>(
                            "Name",
                            new FuncDataTemplate<FileNodeVM>(
                                (x, ns) => new TextBlock
                                {
                                    Text = x?.Name,
                                    Foreground = x?.NameBrushes,
                                })),
                        x => x.Children)
                }
            };
    }

    private void ChangeFileNodeVM(FileNodeVM targetFileNodeVm)
    {
        FileNodeVm.Clear();
        FileNodeVm.Add(targetFileNodeVm);
    }

    #endregion

    #region 功能

    /// <summary>
    /// 可能是用户主动切换，也可能是从repo跳转时自动切换
    /// </summary>
    /// <param name="diskLabel"></param>
    private void ChangeDiskLabel(string diskLabel)
    {
        var found = FileDataVmBundles
            .Find(x => x.FileData.DiskLabel == diskLabel);
        if (found == null)
            return;
        CurrShowFileNodeIndex = FileDataVmBundles.IndexOf(found);
        SelectedDiskLabel = diskLabel;
        ChangeFileNodeVM(found.FileNodeVm);
    }

    private void SelectRepoNode(RepoNode repoNode)
    {
        // 更新显示当前存储了当前repo node的节点
        CurrRepoNodeSaveFileNodes.Clear();
        foreach (var saveFileNodeData in repoNode.SaveFileNodeDatas)
        {
            CurrRepoNodeSaveFileNodes.Add(saveFileNodeData.DiskLabel);
        }

        // 默认选择第一个
        if (CurrRepoNodeSaveFileNodes.Count > 0)
        {
            SelectedSaveFileNodeLabel = CurrRepoNodeSaveFileNodes[0];
        }

        // 浏览模式下，直接给file tree切过去
        if (IsViewMode)
            JumpToSaveFileNode();
        // 编辑模式下，勾选了自动跳转时，直接跳过去
        if (IsEditMode & AutoJumpToSaveFileNode)
            JumpToSaveFileNode();
    }

    private void SelectFileNode(FileNode fileNode)
    {
        if (IsViewMode || (IsEditMode & AutoJumpToDeclareRepoNode))
        {
            // 自动选中对应的声明持有的repo node
            var repoNodePath = fileNode.DeclareRepoNodeDatas
                .FirstOrDefault()
                ?.RepoNodePath ?? string.Empty;
            var target = FindRepoNodeVmByPath(
                RepoNodeVm,
                repoNodePath,
                out var indexPath);
            if (indexPath != null)
            {
                RepoNodeSource.Expand(indexPath.Value);
                RepoNodeSource?.RowSelection?.Select(indexPath.Value);
            }
        }
    }

    public void JumpToSaveFileNode()
    {
        var selectRepoNode = RepoNodeSource
            ?.RowSelection
            ?.SelectedItem
            ?.RepoNode ?? null;
        var selectDiskLabel = SelectedSaveFileNodeLabel;
        if (selectRepoNode == null || string.IsNullOrEmpty(selectDiskLabel))
            return;
        var foundSaveData = selectRepoNode.SaveFileNodeDatas
            .Find(x => x.DiskLabel == selectDiskLabel);
        if (foundSaveData == null)
            return;
        ChangeDiskLabel(selectDiskLabel);
        var target = FindFileNodeVmByPath(
            FileDataVmBundles[CurrShowFileNodeIndex].FileNodeVm,
            foundSaveData.FileNodePath,
            out var indexPath);
        if (indexPath != null)
        {
            CurrFileNodeSource.Expand(indexPath.Value);
            CurrFileNodeSource?.RowSelection?.Select(indexPath.Value);
        }
    }

    #endregion

    #region Utils
    
    private RepoNodeVM? FindRepoNodeVmByPath(
        RepoNodeVM root,
        string path,
        out IndexPath? indexPath)
    {
        var ret = TreeNodeVMBase<RepoNodeVM>.FindTreeNodeVmByPath(
            root,
            path,
            out indexPath);
        return ret as RepoNodeVM;
    }

    private FileNodeVM? FindFileNodeVmByPath(
        FileNodeVM root,
        string path,
        out IndexPath? indexPath)
    {
        var ret = TreeNodeVMBase<FileNodeVM>.FindTreeNodeVmByPath(
            root,
            path,
            out indexPath);
        return ret as FileNodeVM;
    }

    #endregion Utils

    #region 测试功能

    public void ShowRepoNode()
    {
        var rowSelection = RepoNodeSource.RowSelection;
        var select = rowSelection?.SelectedItem ?? null;
        Console.WriteLine(select?.Name ?? "");
    }

    #endregion
}