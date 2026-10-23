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
    private readonly TreeProjection _projection;

    public RepoNode RepoNodeRoot { get; }
    public RepoNodeVM RepoNodeVm { get; }

    [Reactive]
    public HierarchicalTreeDataGridSource<RepoNodeVM> RepoNodeSource { get; set; }

    public ObservableCollection<string> CurrRepoNodeSaveFileNodes { get; } = new();

    public RepoNodeSearchViewModel RepoSearch { get; } = new();

    [Reactive] public RepoNodeVM? CurrentRepoNode { get; private set; }

    public bool HasCurrentRepoNode => CurrentRepoNode != null;
    public bool CurrentRepoNodeIsDirectory => CurrentRepoNode?.IsDirectory ?? true;
    public string CurrentRepoNodeName => CurrentRepoNode?.Name ?? "选择一个目录或文件";
    public string CurrentRepoNodeKind => CurrentRepoNode == null
        ? "选择节点以查看存储位置与验证策略"
        : CurrentRepoNode.IsDirectory ? "虚拟文件夹" : "虚拟文件";
    public string CurrentRepoNodeStrategyName =>
        string.IsNullOrEmpty(CurrentRepoNode?.DeclareHoldingStrategyName)
            ? "未设置"
            : CurrentRepoNode.DeclareHoldingStrategyName;

    public ObservableCollection<StorageLocationViewModel> CurrentStorageLocations { get; } = new();
    public int StorageLocationCount => CurrentStorageLocations.Count;

    [Reactive] public string SelectedSaveFileNodeLabel { get; set; } = string.Empty;

    [Reactive] public string RepoNodePathString { get; set; } = string.Empty;

    public ReactiveCommand<RepoNodeVM, Unit> RepoNodeSelectedCommand { get; set; }
        = ReactiveCommand.Create<RepoNodeVM>(_ => { });

    public ReactiveCommand<string, Unit> RepoNodePathStringChangeCommand { get; set; }
        = ReactiveCommand.Create<string>(_ => { });

    public RepoBrowserViewModel(RepoNode repoNodeRoot, TreeProjection projection)
    {
        _projection = projection;
        RepoNodeRoot = repoNodeRoot;
        RepoNodeVm = projection.CreateRepoTree(repoNodeRoot);
        RepoNodeSource = TreeDataGridSourceFactory.CreateRepoSource(RepoNodeVm);
    }

    public void UpdateCurrentRepoNode(RepoNode repoNode)
    {
        CurrentRepoNode = _projection.GetRepoNodeVm(repoNode);
        RepoNodePathString = repoNode.GetPath();

        CurrRepoNodeSaveFileNodes.Clear();
        CurrentStorageLocations.Clear();
        foreach (var saveFileNodeData in repoNode.SaveFileNodeDatas)
        {
            CurrRepoNodeSaveFileNodes.Add(saveFileNodeData.DiskLabel);
            CurrentStorageLocations.Add(new StorageLocationViewModel(
                saveFileNodeData.DiskLabel,
                saveFileNodeData.FileNodePath));
        }

        if (CurrRepoNodeSaveFileNodes.Count > 0)
            SelectedSaveFileNodeLabel = CurrRepoNodeSaveFileNodes[0];
        else
            SelectedSaveFileNodeLabel = string.Empty;

        RefreshInspector();
    }

    public void ClearCurrentRepoNode()
    {
        CurrentRepoNode = null;
        RepoNodePathString = string.Empty;
        CurrRepoNodeSaveFileNodes.Clear();
        CurrentStorageLocations.Clear();
        SelectedSaveFileNodeLabel = string.Empty;
        RefreshInspector();
    }

    private void RefreshInspector()
    {
        this.RaisePropertyChanged(nameof(HasCurrentRepoNode));
        this.RaisePropertyChanged(nameof(CurrentRepoNodeName));
        this.RaisePropertyChanged(nameof(CurrentRepoNodeKind));
        this.RaisePropertyChanged(nameof(CurrentRepoNodeIsDirectory));
        this.RaisePropertyChanged(nameof(CurrentRepoNodeStrategyName));
        this.RaisePropertyChanged(nameof(StorageLocationCount));
    }
}
