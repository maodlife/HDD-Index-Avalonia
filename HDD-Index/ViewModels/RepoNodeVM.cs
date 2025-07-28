using System.Collections.Generic;
using System.Collections.ObjectModel;
using HDD_Index.Models;

namespace HDD_Index.ViewModels;

public class RepoNodeVM
{
    #region Properties

    public ObservableCollection<RepoNodeVM> Children { get; set; } = new();
    public string Name { get; set; }
    public bool IsDirectory { get; set; }
    public RepoNode RepoNode { get; set; }

    public List<SaveFileNodeData> SaveFileNodeDatas { get; set; } =
        new List<SaveFileNodeData>();

    public int SaveFileNodeCnt => SaveFileNodeDatas.Count;

    public string SaveFileNodeCntString => this.SaveFileNodeCnt > 0
        ? this.SaveFileNodeCnt.ToString()
        : "";

    #endregion

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