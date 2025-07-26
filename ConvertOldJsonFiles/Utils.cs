using HDD_Index.Models;

namespace ConvertOldJsonFiles;

public class Utils
{
    public static RepoNode ConvertRepoNode(OldRepoNode oldRepoNode)
    {
        var node = new RepoNode();
        node.Name = oldRepoNode.name;
        node.IsDirectory = oldRepoNode.isDir;
        foreach (var child in oldRepoNode.childs
                     .Select(ConvertRepoNode))
        {
            node.Children.Add(child);
        }

        foreach (var oldNodeSaveData in oldRepoNode.nodeSaveDatas)
        {
            var saveData = new SaveFileNodeData();
            saveData.DiskLabel = oldNodeSaveData.hddLabel;
            saveData.FileNodePath = oldNodeSaveData.treePath;
            node.SaveFileNodeDatas.Add(saveData);
        }

        return node;
    }
    
    public static FileNode ConvertFileNode(OldFileNode oldFileNode)
    {
        var node = new FileNode();
        node.Name = oldFileNode.name;
        node.IsDirectory = oldFileNode.isDir;
        foreach (var child in oldFileNode.childs
                     .Select(ConvertFileNode))
        {
            node.Children.Add(child);
        }
        
        var saveRepoNodePath = oldFileNode.saveData?.path ?? "";
        if (!string.IsNullOrEmpty(saveRepoNodePath))
        {
            var declareData = new DeclareRepoNodeData();
            declareData.RepoNodePath = saveRepoNodePath;
            node.DeclareRepoNodeDatas.Add(declareData);
        }

        return node;
    }
}