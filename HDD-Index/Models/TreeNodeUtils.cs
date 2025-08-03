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
}