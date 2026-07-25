using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace HDD_Index.ViewModels;

public abstract class TreeNodeVMBase<T> : ViewModelBase where T : TreeNodeVMBase<T>
{
    public abstract string Name { get; }
    public ObservableCollection<T> Children { get; set; }
        = new ObservableCollection<T>();

    public static TreeNodeVMBase<T>? FindTreeNodeVmByPath(
        TreeNodeVMBase<T> root,
        string? path,
        out IndexPath? indexPath)
    {
        indexPath = null;
        if (path == null)
            return null;
        var nameList = path.Split('/');
        if (nameList.Length == 0)
            return null;
        var ret = root;
        if (ret.Name != nameList[0])
            return null;
        List<int> indexes = new List<int>();
        indexes.Add(0);
        for (var i = 1; i < nameList.Length; i++)
        {
            var name = nameList[i];
            for (var j = 0; j < ret.Children.Count; j++)
            {
                var child = ret.Children[j];
                if (child.Name == name)
                {
                    ret = child;
                    indexes.Add(j);
                    break;
                }
            }

            if (ret.Name != name)
            {
                return null;
            }
        }

        indexPath = new IndexPath(indexes);

        return ret;
    }
}
