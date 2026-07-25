namespace ConvertOldJsonFiles;

public class OldFileNode
{
    public string name { get; set; }
    public bool isDir { get; set; }
    public List<OldFileNode> childs { get; set; }
    public DeclareSaveData saveData { get; set; }
}

public class DeclareSaveData
{
    public string path { get; set; }
}
