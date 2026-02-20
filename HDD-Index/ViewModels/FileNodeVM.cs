using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Media;
using HDD_Index.Models;
using ReactiveUI;

namespace HDD_Index.ViewModels;

public class FileNodeVM : TreeNodeVMBase<FileNodeVM>
{
    public bool IsDirectory { get; set; }

    public FileNode FileNode { get; set; }

    public ObservableCollection<DeclareRepoNodeData> DeclareRepoNodeDatas { get; set; } =
        new ObservableCollection<DeclareRepoNodeData>();

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

    public FileNodeVM()
    {
        DeclareRepoNodeDatas.CollectionChanged += DeclareRepoNodeDatas_CollectionChanged;
    }

    private void DeclareRepoNodeDatas_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.RaisePropertyChanged(nameof(NameBrushes));
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