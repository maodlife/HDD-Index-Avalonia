using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HDD_Index.Models;

/// <summary>
/// 虚拟仓库(repository)的节点数据结构
/// </summary>
public class RepoNode : TreeNodeBase
{
    public List<SaveFileNodeData> SaveFileNodeDatas { get; set; } =
        new List<SaveFileNodeData>();

    public static RepoNode? CreateByJson(string json)
    {
        var root = JsonSerializer.Deserialize<RepoNode>(json);
        root?.SetParent();
        return root;
    }
}

/// <summary>
/// 用于存储这个节点(及其子树)存储在哪个FileNode的信息
/// </summary>
public class SaveFileNodeData : ICloneable
{
    public string DiskLabel { get; set; }
    public string FileNodePath { get; set; }
    
    public object Clone()
    {
        return this.MemberwiseClone();
    }
}