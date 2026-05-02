using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using HDD_Index.Models;
using ReactiveUI;

namespace HDD_Index.ViewModels;

public class RepoNodeVM : TreeNodeVMBase<RepoNodeVM>
{
    #region Properties

    public bool IsDirectory { get; set; }
    public RepoNode RepoNode { get; set; }

    public ObservableCollection<SaveFileNodeData> SaveFileNodeDatas { get; set; } =
        new ObservableCollection<SaveFileNodeData>();

    public int SaveFileNodeCnt => SaveFileNodeDatas.Count;

    public string SaveFileNodeCntString => this.SaveFileNodeCnt > 0
        ? this.SaveFileNodeCnt.ToString()
        : "";

    public string DeclareHoldingStrategyName =>
        RepoNode.DeclareHoldingStrategyType == null
            ? string.Empty
            : DeclareHoldingStrategyFactory
                .Create(RepoNode.DeclareHoldingStrategyType.Value)
                .Name;

    #endregion

    public RepoNodeVM()
    {
        SaveFileNodeDatas.CollectionChanged += SaveFileNodeDatas_CollectionChanged;
    }

    private void SaveFileNodeDatas_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.RaisePropertyChanged(nameof(SaveFileNodeCnt));
        this.RaisePropertyChanged(nameof(SaveFileNodeCntString));
    }

    public void RefreshDeclareHoldingStrategyName()
    {
        this.RaisePropertyChanged(nameof(DeclareHoldingStrategyName));
    }

    public static RepoNodeVM Create(RepoNode repoNode)
    {
        var vm = new RepoNodeVM
        {
            Name = repoNode.Name,
            IsDirectory = repoNode.IsDirectory,
            RepoNode = repoNode
        };
        foreach (var child in repoNode.Children)
        {
            var childVm = Create(child as RepoNode);
            vm.Children.Add(childVm);
        }

        foreach (var item in repoNode.SaveFileNodeDatas)
        {
            vm.SaveFileNodeDatas.Add((SaveFileNodeData)item.Clone());
        }

        return vm;
    }
}