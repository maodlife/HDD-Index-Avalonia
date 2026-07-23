using System.Collections.Generic;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;
using ReactiveUI;

namespace HDD_Index.ViewModels;

public class RepoNodeVM : TreeNodeVMBase<RepoNodeVM>
{
    #region Properties

    public override string Name => RepoNode.Name;
    public bool IsDirectory => RepoNode.IsDirectory;
    public RepoNode RepoNode { get; }

    public IReadOnlyList<SaveFileNodeData> SaveFileNodeDatas
        => RepoNode.SaveFileNodeDatas;

    public int SaveFileNodeCnt => RepoNode.SaveFileNodeDatas.Count;

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

    public RepoNodeVM(RepoNode repoNode)
    {
        RepoNode = repoNode;
    }

    public void Refresh(TreeNodePresentation presentation)
    {
        if ((presentation & TreeNodePresentation.Name) != 0)
        {
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(IsDirectory));
        }
        if ((presentation & TreeNodePresentation.Relationships) != 0)
        {
            this.RaisePropertyChanged(nameof(SaveFileNodeCnt));
            this.RaisePropertyChanged(nameof(SaveFileNodeCntString));
        }
        if ((presentation & TreeNodePresentation.Strategy) != 0)
            this.RaisePropertyChanged(nameof(DeclareHoldingStrategyName));
    }

    public static RepoNodeVM Create(RepoNode repoNode)
    {
        return new TreeProjection().CreateRepoTree(repoNode);
    }
}
