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

    public HierarchicalTreeDataGridSource<RepoNodeVM> RepoNodeSource
    {
        get;
        set;
    }

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

    #endregion

    #region 初始化

    public MainWindowViewModel()
    {
        InitCommand();
        InitRepoData();
        InitFileData();
    }

    private void InitCommand()
    {
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
        SelectedDiskLabel = diskLabel;
        CurrFileNodeSource = found.RepoNodeSource;
    }

    #endregion

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