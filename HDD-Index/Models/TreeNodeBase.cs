using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HDD_Index.Models;

[JsonDerivedType(typeof(RepoNode), typeDiscriminator: "repoNode")]
[JsonDerivedType(typeof(FileNode), typeDiscriminator: "fileNode")]
public class TreeNodeBase
{
    [JsonIgnore] public TreeNodeBase Parent { get; set; }
    public List<TreeNodeBase> Children { get; set; } =  new List<TreeNodeBase>();
    public string Name { get; set; }
    public bool IsDirectory { get; set; }
}