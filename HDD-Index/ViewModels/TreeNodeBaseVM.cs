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

    public static TreeNodeBaseVM Create(TreeNodeBase treeNode)
    {
        var vm = new TreeNodeBaseVM();
        vm.Name = treeNode.Name;
        vm.IsDirectory = treeNode.IsDirectory;
        vm.TreeNode = treeNode;
        foreach (var child in treeNode.Children)
        {
            var childVm = Create(child);
            vm.Children.Add(childVm);
        }

        return vm;
    }
}
