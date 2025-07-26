using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using HDD_Index.Models;
using ReactiveUI;

namespace HDD_Index.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public RepoNode RepoNodeRoot { get; set; }
    public TreeNodeBaseVM RepoNodeVm { get; set; }

    public HierarchicalTreeDataGridSource<TreeNodeBaseVM> RepoNodeSource
    {
        get;
        set;
    }

    public MainWindowViewModel()
    {
        InitRepoData();
    }

    private void InitRepoData()
    {
        var repoNodeFilePath =
            "/Users/maodlife/Documents/HDD-Index/config/RepoTreeData.txt";
        var json = File.ReadAllText(repoNodeFilePath);
        RepoNodeRoot = JsonSerializer.Deserialize<RepoNode>(json);

        RepoNodeVm = TreeNodeBaseVM.Create(RepoNodeRoot);

        RepoNodeSource =
            new HierarchicalTreeDataGridSource<TreeNodeBaseVM>(RepoNodeVm)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<TreeNodeBaseVM>(
                        new TextColumn<TreeNodeBaseVM, string>("Name",
                            x => x.Name),
                        x => x.Children),
                    // new TextColumn<TreeNodeBase,string>("存储数",
                    //     x => x.)
                }
            };
    }

    public void ShowRepoNode()
    {
        var rowSelection = RepoNodeSource.RowSelection;
        var select = rowSelection?.SelectedItem ?? null;
        Console.WriteLine(select?.Name ?? "");
    }
}