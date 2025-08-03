using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using DynamicData;
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

    /// <summary>
    /// 当前实际用于View动态绑定的source
    /// </summary>
    [Reactive]
    public HierarchicalTreeDataGridSource<FileNodeVM> CurrFileNodeSource
    {
        get;
        set;
    }

    [Reactive]
    public ObservableCollection<string> DiskLabels { get; set; } =
        new ObservableCollection<string>();

    [Reactive] public string SelectedDiskLabel { get; set; }

    public ReactiveCommand<string, Unit> DiskLabelSelectedCommand { get; set; }

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
            CurrFileNodeSource = FileDataVmBundles[0].RepoNodeSource;
            SelectedDiskLabel = DiskLabels[0];
        }
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
        CurrFileNodeSource = found.RepoNodeSource;
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

    private FileNodeVM? FindFileNodeVmByPath(
        FileNodeVM root,
        string path,
        out IndexPath? indexPath)
    {
        var nameList = path.Split('/');
        indexPath = null;
        if (nameList.Length == 0)
            return null;
        var ret = root;
        if (ret.Name != nameList[0])
            return null;
        List<int> indexes = new List<int>();
        indexes.Add(0);
        for (var i = 1; i < nameList.Length; i++)
        {
            var name = nameList[i];
            for (var j = 0; j < ret.Children.Count; j++)
            {
                var child = ret.Children[j];
                if (child.Name == name)
                {
                    ret = child;
                    indexes.Add(j);
                    break;
                }
            }

            if (ret.Name != name)
            {
                return null;
            }
        }

        indexPath = new IndexPath(indexes);
        return ret;
    }

    #endregion Utils

    #region 测试功能

    public void ShowRepoNode()
    {
        var rowSelection = RepoNodeSource.RowSelection;
        var select = rowSelection?.SelectedItem ?? null;
        Console.WriteLine(select?.Name ?? "");
    }

    public void ChangeNextDisk()
    {
        if (CurrShowFileNodeIndex + 1 < FileDataVmBundles.Count)
        {
            CurrShowFileNodeIndex++;
        }
        else
        {
            CurrShowFileNodeIndex = 0;
        }

        CurrFileNodeSource =
            FileDataVmBundles[CurrShowFileNodeIndex].RepoNodeSource;
    }

    #endregion
}