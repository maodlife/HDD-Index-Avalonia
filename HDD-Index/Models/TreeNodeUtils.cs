using System.Linq;
using System.Text.Json;

namespace HDD_Index.Models;

public static class TreeNodeUtils
{
    public static TreeNodeBase? GetNodeByPathFromRoot(
        TreeNodeBase root, string path)
    {
        var ret = root;
        var nameList = path.Split('/');
        if (ret.Name != nameList[0])
            return null;
        for (var i = 1; i < nameList.Length; i++)
        {
            var found = ret.Children
                .Find(x => x.Name == nameList[i]);
            if (found == null)
                return null;
            ret = found;
        }
        return ret;
    }

    /// <summary>
    /// 检查 RepoNode 的树结构是否完全被包含在 FileNode 的树结构中（用于判断声明持有是否成立）
    /// </summary>
    /// <param name="repoNode">要检查的 RepoNode</param>
    /// <param name="fileNode">要检查的 FileNode</param>
    /// <returns>如果 fileNode 包含了 repoNode 及其所有子节点结构，则返回 true；否则返回 false</returns>
    public static bool CheckDeclarationStatus(RepoNode repoNode, FileNode fileNode)
    {
        if (repoNode == null || fileNode == null)
            return false;

        var strategyType = repoNode.DeclareHoldingStrategyType
                           ?? DeclareHoldingStrategyType.Default;
        return DeclareHoldingStrategyFactory
            .Create(strategyType)
            .CheckDeclareHolding(repoNode, fileNode, out _);
    }
}