using System.Collections.Generic;
using Avalonia.Media;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;
using ReactiveUI;

namespace HDD_Index.ViewModels;

public class FileNodeVM : TreeNodeVMBase<FileNodeVM>
{
    public override string Name => FileNode.Name;
    public bool IsDirectory => FileNode.IsDirectory;

    public FileNode FileNode { get; }

    public IReadOnlyList<DeclareRepoNodeData> DeclareRepoNodeDatas
        => FileNode.DeclareRepoNodeDatas;

    public IBrush NameBrushes
    {
        get
        {
            if (FileNode.DeclareRepoNodeDatas.Count == 0)
                return Brushes.Black;
            else
                return Brushes.Green;
        }
    }

    public FileNodeVM(FileNode fileNode)
    {
        FileNode = fileNode;
    }

    public void Refresh(TreeNodePresentation presentation)
    {
        if ((presentation & TreeNodePresentation.Name) != 0)
        {
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(IsDirectory));
        }
        if ((presentation & TreeNodePresentation.Relationships) != 0)
            this.RaisePropertyChanged(nameof(NameBrushes));
    }

    public static FileNodeVM Create(FileNode fileNode)
    {
        return new TreeProjection().CreateFileTree(fileNode);
    }
}
