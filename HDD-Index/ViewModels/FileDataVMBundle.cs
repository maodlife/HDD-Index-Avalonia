using System;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using HDD_Index.Models;

namespace HDD_Index.ViewModels;

public class FileDataVMBundle
{
    public FileData FileData { get; set; }
    public FileNodeVM FileNodeVm { get; set; }

    public static FileDataVMBundle Create(
        string diskLabel,
        string json,
        string localFolderPath = "")
    {
        var bundle = new FileDataVMBundle();
        bundle.FileData = new FileData();
        bundle.FileData.DiskLabel = diskLabel;
        bundle.FileData.LocalFolderPath = localFolderPath;
        var root = FileNode.CreateByJson(json);
        if (root != null)
        {
            bundle.FileData.FileNodeRoot = root;
            bundle.FileNodeVm = FileNodeVM.Create(bundle.FileData.FileNodeRoot);
        }
        return bundle;
    }
    
    public static FileDataVMBundle CreateByPath(
        string diskLabel,
        string path,
        string jsonFilePath)
    {
        var bundle = new FileDataVMBundle();
        bundle.FileData = new FileData();
        bundle.FileData.DiskLabel = diskLabel;
        bundle.FileData.LocalFolderPath = path;
        bundle.FileData.JsonFilePath = jsonFilePath;
        var root = FileNode.CreateByPath(path);
        if (root == null)
            throw new InvalidOperationException($"无法创建文件树: {path}");

        bundle.FileData.FileNodeRoot = root;
        bundle.FileNodeVm = FileNodeVM.Create(bundle.FileData.FileNodeRoot);
        return bundle;
    }
}
