using System;

namespace HDD_Index.ViewModels;

/// <summary>A saved relationship in the index, not a claim that the disk is online.</summary>
public sealed class StorageLocationViewModel : ViewModelBase
{
    public StorageLocationViewModel(string diskLabel, string fileNodePath)
    {
        DiskLabel = diskLabel;
        FileNodePath = fileNodePath;
    }

    public string DiskLabel { get; }
    public string FileNodePath { get; }

    // The disk label is shown separately; preserve paths whose root has a different name.
    public string DisplayPath => FileNodePath.StartsWith(DiskLabel + "/", StringComparison.Ordinal)
        ? FileNodePath[(DiskLabel.Length + 1)..]
        : FileNodePath;
}
