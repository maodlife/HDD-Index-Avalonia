using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HDD_Index.Models;

[JsonDerivedType(typeof(RepoNode), typeDiscriminator: "repoNode")]
[JsonDerivedType(typeof(FileNode), typeDiscriminator: "fileNode")]
public class TreeNodeBase
{
    [JsonIgnore] public TreeNodeBase? Parent { get; set; }
    public List<TreeNodeBase> Children { get; set; } =  new List<TreeNodeBase>();
    public string Name { get; set; }
    public bool IsDirectory { get; set; }

    protected void SetParent()
    {
        foreach (var child in Children)
        {
            child.Parent = this;
            child.SetParent();
        }
    }

    public string GetPath()
    {
        List<string> pathList = new List<string>();
        TreeNodeBase? parent = this;
        while (parent != null)
        {
            pathList.Add(parent.Name);
            parent = parent.Parent;
        }
        pathList.Reverse();
        return string.Join("/", pathList);
    }
}