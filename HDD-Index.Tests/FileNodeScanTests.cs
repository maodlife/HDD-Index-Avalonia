using HDD_Index.Models;

namespace HDD_Index.Tests;

public class FileNodeScanTests
{
    [Fact]
    public void CreateByPath_WithoutProgressKeepsExistingBehavior()
    {
        using var tempFolder = new TempFolder();
        File.WriteAllText(Path.Combine(tempFolder.Path, "file.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(tempFolder.Path, "folder"));

        var root = FileNode.CreateByPath(tempFolder.Path);

        Assert.NotNull(root);
        Assert.True(root.IsDirectory);
        Assert.Equal(Path.GetFileName(tempFolder.Path), root.Name);
        Assert.Contains(root.Children.OfType<FileNode>(), x => x.Name == "file.txt");
        Assert.Contains(root.Children.OfType<FileNode>(), x => x.Name == "folder");
    }

    [Fact]
    public void CreateByPath_ReportsTopLevelVisibleEntryProgress()
    {
        using var tempFolder = new TempFolder();
        File.WriteAllText(Path.Combine(tempFolder.Path, "a.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.Path, "b.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.Path, ".hidden"), string.Empty);
        var childFolder = Directory.CreateDirectory(Path.Combine(tempFolder.Path, "folder"));
        File.WriteAllText(Path.Combine(childFolder.FullName, "nested.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(tempFolder.Path, ".hidden-folder"));
        var progress = new CapturingProgress();

        var root = FileNode.CreateByPath(
            tempFolder.Path,
            progress,
            CancellationToken.None);

        Assert.NotNull(root);
        Assert.Equal(3, progress.Items.Max(x => x.TotalTopLevelEntries));
        Assert.Equal(3, progress.Items.Max(x => x.CompletedTopLevelEntries));
        Assert.DoesNotContain(root.Children.OfType<FileNode>(), x => x.Name == ".hidden");
        Assert.DoesNotContain(root.Children.OfType<FileNode>(), x => x.Name == ".hidden-folder");
    }

    [Fact]
    public void CreateByPath_ThrowsWhenCancelledDuringScan()
    {
        using var tempFolder = new TempFolder();
        File.WriteAllText(Path.Combine(tempFolder.Path, "a.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.Path, "b.txt"), string.Empty);
        using var cancellationTokenSource = new CancellationTokenSource();
        var progress = new CapturingProgress(item =>
        {
            if (item.CompletedTopLevelEntries == 1)
                cancellationTokenSource.Cancel();
        });

        Assert.Throws<OperationCanceledException>(() =>
            FileNode.CreateByPath(
                tempFolder.Path,
                progress,
                cancellationTokenSource.Token));
    }

    private sealed class CapturingProgress : IProgress<FileNodeScanProgress>
    {
        private readonly Action<FileNodeScanProgress>? _onReport;

        public CapturingProgress(Action<FileNodeScanProgress>? onReport = null)
        {
            _onReport = onReport;
        }

        public List<FileNodeScanProgress> Items { get; } = new();

        public void Report(FileNodeScanProgress value)
        {
            Items.Add(value);
            _onReport?.Invoke(value);
        }
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"hdd-index-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
