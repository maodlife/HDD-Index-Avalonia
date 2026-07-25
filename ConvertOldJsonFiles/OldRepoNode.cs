namespace ConvertOldJsonFiles;

public class OldRepoNode
{
    public string name { get; set; }
    public bool isDir { get; set; }
    public List<OldRepoNode> childs { get; set; }
    public List<NodeSaveData> nodeSaveDatas { get; set; }
}

public class NodeSaveData
{
    public string hddLabel { get; set; }
    public string treePath { get; set; }
}
