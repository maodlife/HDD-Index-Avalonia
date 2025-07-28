using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using HDD_Index.Models;

namespace HDD_Index.ViewModels;

public class FileDataVMBundle
{
    public FileData FileData { get; set; }
    public FileNodeVM FileNodeVm { get; set; }
    public HierarchicalTreeDataGridSource<FileNodeVM> RepoNodeSource
    {
        get;
        set;
    }

    public static FileDataVMBundle Create(string diskLabel, string json)
    {
        var bundle = new FileDataVMBundle();
        bundle.FileData = new FileData();
        bundle.FileData.DiskLabel = diskLabel;
        bundle.FileData.FileNodeRoot =
            JsonSerializer.Deserialize<FileNode>(json);
        bundle.FileNodeVm = FileNodeVM.Create(bundle.FileData.FileNodeRoot);
        bundle.RepoNodeSource =
            new HierarchicalTreeDataGridSource<FileNodeVM>(bundle.FileNodeVm)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<FileNodeVM>(
                        new TextColumn<FileNodeVM, string>("Name",
                            x => x.Name),
                        x => x.Children),
                }
            };
        return bundle;
    }
}