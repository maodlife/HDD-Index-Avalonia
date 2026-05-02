using HDD_Index.Services;

namespace HDD_Index.Tests;

public class DirtyJsonFileTrackerTests
{
    [Fact]
    public void MarksAndClearsDirtyJsonFiles()
    {
        var tracker = new DirtyJsonFileTracker();
        tracker.SetRepoFilePath(@"C:\data\repo.json");
        tracker.SetFileNodePath("DiskA", @"C:\data\disk-a.json");
        tracker.SetFileNodePath("DiskB", @"C:\data\disk-b.json");

        tracker.MarkRepoDirty();
        tracker.MarkFileDirty("DiskA");

        Assert.True(tracker.HasDirtyFiles);
        Assert.Equal(
            new[] { @"C:\data\disk-a.json", @"C:\data\repo.json" },
            tracker.GetDirtyFilePaths());

        tracker.ClearDirtyFiles(new[] { @"C:\data\repo.json" });

        Assert.True(tracker.HasDirtyFiles);
        Assert.Equal(new[] { @"C:\data\disk-a.json" }, tracker.GetDirtyFilePaths());

        tracker.ClearDirtyFiles(new[] { @"C:\data\disk-a.json" });

        Assert.False(tracker.HasDirtyFiles);
    }

    [Fact]
    public void MarkAllFileNodesDirty_DoesNotMarkRepo()
    {
        var tracker = new DirtyJsonFileTracker();
        tracker.SetRepoFilePath(@"C:\data\repo.json");
        tracker.SetFileNodePath("DiskA", @"C:\data\disk-a.json");
        tracker.SetFileNodePath("DiskB", @"C:\data\disk-b.json");

        tracker.MarkAllFileNodesDirty();

        Assert.Equal(
            new[] { @"C:\data\disk-a.json", @"C:\data\disk-b.json" },
            tracker.GetDirtyFilePaths());
    }
}
