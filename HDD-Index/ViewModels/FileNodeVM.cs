using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using HDD_Index.Models;

namespace HDD_Index.ViewModels;

public class FileNodeVM
{
    public ObservableCollection<FileNodeVM> Children { get; set; } = new();

    public string Name { get; set; }
    public bool IsDirectory { get; set; }

    public FileNode FileNode { get; set; }

    public List<DeclareRepoNodeData> DeclareRepoNodeDatas { get; set; } =
        new List<DeclareRepoNodeData>();

    public IBrush NameBrushes
    {
        get
        {
            if (DeclareRepoNodeDatas.Count == 0)
                return Brushes.Black;
            else
                return Brushes.Green;
        }
    }

    public static FileNodeVM Create(FileNode fileNode)
    {
        var vm = new FileNodeVM()
        {
            Name = fileNode.Name,
            IsDirectory = fileNode.IsDirectory,
            FileNode = fileNode
        };
        foreach (var child in fileNode.Children)
        {
            var childVm = Create(child as FileNode);
            vm.Children.Add(childVm);
        }

        foreach (var declareRepoNodeData in fileNode.DeclareRepoNodeDatas)
        {
            vm.DeclareRepoNodeDatas.Add(
                (DeclareRepoNodeData)declareRepoNodeData.Clone());
        }

        return vm;
    }
}