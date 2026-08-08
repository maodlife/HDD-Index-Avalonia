using System;
using System.Collections.Generic;

namespace HDD_Index.Models;

/// <summary>
/// 磁盘文件的节点数据结构
/// </summary>
public class FileNode : TreeNodeBase
{
    /// <summary>
    /// 设计上允许一个磁盘文件声明持有多个Repository中的节点，
    /// 从而方便在Repository中整理分类
    /// </summary>
    public List<DeclareRepoNodeData> DeclareRepoNodeDatas { get; set; } =
        new List<DeclareRepoNodeData>();

}

/// <summary>
/// 存储这个磁盘文件节点对应声明持有了哪个Repository中的节点
/// </summary>
public class DeclareRepoNodeData : ICloneable
{
    public string RepoNodePath { get; set; } = string.Empty;

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}
