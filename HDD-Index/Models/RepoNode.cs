using System;
using System.Collections.Generic;

namespace HDD_Index.Models;

/// <summary>
/// 虚拟仓库(repository)的节点数据结构
/// </summary>
public class RepoNode : TreeNodeBase
{
    public DeclareHoldingStrategyType? DeclareHoldingStrategyType { get; set; }

    public List<SaveFileNodeData> SaveFileNodeDatas { get; set; } =
        new List<SaveFileNodeData>();

}

/// <summary>
/// 用于存储这个节点(及其子树)存储在哪个FileNode的信息
/// </summary>
public class SaveFileNodeData : ICloneable
{
    public string DiskLabel { get; set; } = string.Empty;
    public string FileNodePath { get; set; } = string.Empty;

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}
