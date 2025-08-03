using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using HDD_Index.Models;

namespace HDD_Index.ViewModels;

public class FileDataVMBundle
{
    public FileData FileData { get; set; }
    public FileNodeVM FileNodeVm { get; set; }

    public static FileDataVMBundle Create(string diskLabel, string json)
    {
        var bundle = new FileDataVMBundle();
        bundle.FileData = new FileData();
        bundle.FileData.DiskLabel = diskLabel;
        bundle.FileData.FileNodeRoot =
            JsonSerializer.Deserialize<FileNode>(json);
        bundle.FileNodeVm = FileNodeVM.Create(bundle.FileData.FileNodeRoot);
        return bundle;
    }
}