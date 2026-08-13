using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using HDD_Index.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class FileBrowserViewModel : ViewModelBase
{
    private readonly TreeProjection _projection;

    public List<FileData> FileDatas { get; }

    public int CurrShowFileNodeIndex { get; private set; }

    public ObservableCollection<FileNodeVM> FileNodeVm { get; } = new();

    [Reactive]
    public HierarchicalTreeDataGridSource<FileNodeVM> CurrFileNodeSource { get; set; }

    [Reactive]
    public ObservableCollection<string> DiskLabels { get; set; } = new();

    [Reactive] public string SelectedDiskLabel { get; set; } = string.Empty;

    [Reactive] public bool HasCurrentLocalFolderPath { get; set; }

    [Reactive] public bool HasSelectedFileNode { get; set; }

    public ReactiveCommand<FileNodeVM, Unit> FileNodeSelectedCommand { get; set; }
        = ReactiveCommand.Create<FileNodeVM>(_ => { });

    public ReactiveCommand<string, Unit> DiskLabelSelectedCommand { get; set; }
        = ReactiveCommand.Create<string>(_ => { });

    public FileBrowserViewModel(
        List<FileData> fileDatas,
        TreeProjection projection)
    {
        FileDatas = fileDatas;
        _projection = projection;
        foreach (var item in FileDatas)
            DiskLabels.Add(item.DiskLabel);

        if (FileDatas.Count > 0)
        {
            ChangeFileNodeVM(_projection.CreateFileTree(FileDatas[0].FileNodeRoot));
            SelectedDiskLabel = DiskLabels[0];
        }

        CurrFileNodeSource = TreeDataGridSourceFactory.CreateFileSource(FileNodeVm);
        UpdateCurrentLocalFolderPathState();
    }

    public FileData? CurrentFileData =>
        CurrShowFileNodeIndex >= 0 && CurrShowFileNodeIndex < FileDatas.Count
            ? FileDatas[CurrShowFileNodeIndex]
            : null;

    public bool ChangeDiskLabel(string diskLabel)
    {
        var found = FileDatas.Find(x => x.DiskLabel == diskLabel);
        if (found == null)
            return false;

        CurrShowFileNodeIndex = FileDatas.IndexOf(found);
        SelectedDiskLabel = diskLabel;
        var rootVm = _projection.TryGetFileNodeVm(found.FileNodeRoot, out var existing)
            ? existing!
            : _projection.CreateFileTree(found.FileNodeRoot);
        ChangeFileNodeVM(rootVm);
        UpdateCurrentLocalFolderPathState();
        return true;
    }

    public void AddFileData(FileData fileData)
    {
        if (!FileDatas.Contains(fileData))
            FileDatas.Add(fileData);
        _projection.CreateFileTree(fileData.FileNodeRoot);
        if (!DiskLabels.Contains(fileData.DiskLabel))
            DiskLabels.Add(fileData.DiskLabel);
        UpdateCurrentLocalFolderPathState();
    }

    private void ChangeFileNodeVM(FileNodeVM targetFileNodeVm)
    {
        FileNodeVm.Clear();
        FileNodeVm.Add(targetFileNodeVm);
    }

    private void UpdateCurrentLocalFolderPathState()
    {
        HasCurrentLocalFolderPath =
            !string.IsNullOrWhiteSpace(CurrentFileData?.LocalFolderPath);
    }
}
