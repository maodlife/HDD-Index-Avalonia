using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class FileBrowserViewModel : ViewModelBase
{
    public List<FileDataVMBundle> FileDataVmBundles { get; }

    public int CurrShowFileNodeIndex { get; private set; }

    public ObservableCollection<FileNodeVM> FileNodeVm { get; } = new();

    [Reactive]
    public HierarchicalTreeDataGridSource<FileNodeVM> CurrFileNodeSource { get; set; }

    [Reactive]
    public ObservableCollection<string> DiskLabels { get; set; } = new();

    [Reactive] public string SelectedDiskLabel { get; set; } = string.Empty;

    public ReactiveCommand<FileNodeVM, Unit> FileNodeSelectedCommand { get; set; }
        = ReactiveCommand.Create<FileNodeVM>(_ => { });

    public ReactiveCommand<string, Unit> DiskLabelSelectedCommand { get; set; }
        = ReactiveCommand.Create<string>(_ => { });

    public FileBrowserViewModel(List<FileDataVMBundle> fileDataVmBundles)
    {
        FileDataVmBundles = fileDataVmBundles;
        foreach (var item in FileDataVmBundles)
            DiskLabels.Add(item.FileData.DiskLabel);

        if (FileDataVmBundles.Count > 0)
        {
            ChangeFileNodeVM(FileDataVmBundles[0].FileNodeVm);
            SelectedDiskLabel = DiskLabels[0];
        }

        CurrFileNodeSource = TreeDataGridSourceFactory.CreateFileSource(FileNodeVm);
    }

    public bool ChangeDiskLabel(string diskLabel)
    {
        var found = FileDataVmBundles
            .Find(x => x.FileData.DiskLabel == diskLabel);
        if (found == null)
            return false;

        CurrShowFileNodeIndex = FileDataVmBundles.IndexOf(found);
        SelectedDiskLabel = diskLabel;
        ChangeFileNodeVM(found.FileNodeVm);
        return true;
    }

    public void AddBundle(FileDataVMBundle bundle)
    {
        FileDataVmBundles.Add(bundle);
        DiskLabels.Add(bundle.FileData.DiskLabel);
    }

    private void ChangeFileNodeVM(FileNodeVM targetFileNodeVm)
    {
        FileNodeVm.Clear();
        FileNodeVm.Add(targetFileNodeVm);
    }
}
