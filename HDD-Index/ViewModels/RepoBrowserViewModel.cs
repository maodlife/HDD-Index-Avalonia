using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using HDD_Index.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class RepoBrowserViewModel : ViewModelBase
{
    public RepoNode RepoNodeRoot { get; }
    public RepoNodeVM RepoNodeVm { get; }

    [Reactive]
    public HierarchicalTreeDataGridSource<RepoNodeVM> RepoNodeSource { get; set; }

    public ObservableCollection<string> CurrRepoNodeSaveFileNodes { get; } = new();

    public RepoNodeSearchViewModel RepoSearch { get; } = new();

    [Reactive] public string SelectedSaveFileNodeLabel { get; set; } = string.Empty;

    [Reactive] public string RepoNodePathString { get; set; } = string.Empty;

    public ReactiveCommand<RepoNodeVM, Unit> RepoNodeSelectedCommand { get; set; }
        = ReactiveCommand.Create<RepoNodeVM>(_ => { });

    public ReactiveCommand<string, Unit> RepoNodePathStringChangeCommand { get; set; }
        = ReactiveCommand.Create<string>(_ => { });

    public RepoBrowserViewModel(RepoNode repoNodeRoot)
    {
        RepoNodeRoot = repoNodeRoot;
        RepoNodeVm = RepoNodeVM.Create(repoNodeRoot);
        RepoNodeSource = TreeDataGridSourceFactory.CreateRepoSource(RepoNodeVm);
    }

    public void UpdateCurrentRepoNode(RepoNode repoNode)
    {
        RepoNodePathString = repoNode.GetPath();

        CurrRepoNodeSaveFileNodes.Clear();
        foreach (var saveFileNodeData in repoNode.SaveFileNodeDatas)
            CurrRepoNodeSaveFileNodes.Add(saveFileNodeData.DiskLabel);

        if (CurrRepoNodeSaveFileNodes.Count > 0)
            SelectedSaveFileNodeLabel = CurrRepoNodeSaveFileNodes[0];
        else
            SelectedSaveFileNodeLabel = string.Empty;
    }
}
