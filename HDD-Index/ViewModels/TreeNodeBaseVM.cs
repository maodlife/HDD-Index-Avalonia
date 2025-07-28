using System.Collections.Generic;
using System.Collections.ObjectModel;
using HDD_Index.Models;

namespace HDD_Index.ViewModels;

/// <summary>
/// 给ViewModel动态绑定用的数据结构
/// </summary>
public class TreeNodeBaseVM
{
    public ObservableCollection<TreeNodeBaseVM> Children { get; set; } = new();

    public string Name { get; set; }
    public bool IsDirectory { get; set; }

    public TreeNodeBase TreeNode { get; set; }

    public static T Create<T>(TreeNodeBase treeNode)
        where T : TreeNodeBaseVM, new()
    {
        var vm = new T
        {
            Name = treeNode.Name,
            IsDirectory = treeNode.IsDirectory,
            TreeNode = treeNode
        };
        foreach (var child in treeNode.Children)
        {
            var childVm = Create<T>(child);
            vm.Children.Add(childVm);
        }

        return vm;
    }

    protected virtual void AddChild(TreeNodeBase child)
    {
        
    }
}
