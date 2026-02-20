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

        // 如果名字不同，则显然不匹配
        // (注：如果是挂载的根节点可能名字不同，但按照策划案“相同层级相同名字的出现”，这里认为名字必须相同。
        // 或者根节点由调用者保证匹配，这里递归检查子节点)。
        if (repoNode.Name != fileNode.Name)
            return false;

        // Repo 结点树中递归的每一个节点（即整棵树）都被 FileNode 节点树所包含
        foreach (var repoChild in repoNode.Children.OfType<RepoNode>())
        {
            // 在 FileNode 中寻找同名节点
            var matchingFileChild = fileNode.Children.OfType<FileNode>()
                .FirstOrDefault(f => f.Name == repoChild.Name);

            // 如果找不到同名的对应节点，说明未包含，声明持有失败
            if (matchingFileChild == null)
                return false;

            // 递归检查子节点
            if (!CheckDeclarationStatus(repoChild, matchingFileChild))
                return false;
        }

        // 所有 RepoNode 的子节点都能在 FileNode 中找到对应的匹配，且其子树也匹配
        // 允许 FileNode 中有多余的文件
        return true;
    }
}