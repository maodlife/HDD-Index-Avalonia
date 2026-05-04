using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

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

    [Fact]
    public void CreateByPathSkippingDeclaredSubtrees_PreservesDeclaredChildSubtree()
    {
        using var tempFolder = new TempFolder();
        var savedFolderPath = Directory.CreateDirectory(
            Path.Combine(tempFolder.Path, "Saved"));
        File.WriteAllText(Path.Combine(savedFolderPath.FullName, "new.txt"), string.Empty);
        var freshFolderPath = Directory.CreateDirectory(
            Path.Combine(tempFolder.Path, "Fresh"));
        File.WriteAllText(Path.Combine(freshFolderPath.FullName, "fresh.txt"), string.Empty);

        var currentRoot = TestTreeFactory.File(
            Path.GetFileName(tempFolder.Path),
            TestTreeFactory.File(
                "Saved",
                TestTreeFactory.DiskFile("old.txt")));
        var savedNode = (FileNode)currentRoot.Children[0];
        savedNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Repo/Saved"
        });

        var scannedRoot = FileNode.CreateByPathSkippingDeclaredSubtrees(
            tempFolder.Path,
            currentRoot,
            progress: null,
            CancellationToken.None);

        Assert.NotNull(scannedRoot);
        var scannedSaved = Assert.Single(
            scannedRoot.Children.OfType<FileNode>(),
            x => x.Name == "Saved");
        Assert.Single(scannedSaved.DeclareRepoNodeDatas);
        Assert.Contains(scannedSaved.Children.OfType<FileNode>(), x => x.Name == "old.txt");
        Assert.DoesNotContain(scannedSaved.Children.OfType<FileNode>(), x => x.Name == "new.txt");

        var scannedFresh = Assert.Single(
            scannedRoot.Children.OfType<FileNode>(),
            x => x.Name == "Fresh");
        Assert.Contains(scannedFresh.Children.OfType<FileNode>(), x => x.Name == "fresh.txt");
    }

    [Fact]
    public void CreateByPathSkippingDeclaredSubtrees_MissingDeclaredChildBecomesRefreshFailure()
    {
        using var tempFolder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(tempFolder.Path, "Fresh"));
        var repoRoot = TestTreeFactory.Repo("Repo", TestTreeFactory.Repo("Saved"));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var currentRoot = TestTreeFactory.File(
            Path.GetFileName(tempFolder.Path),
            TestTreeFactory.File(
                "Saved",
                TestTreeFactory.DiskFile("old.txt")));
        var savedNode = (FileNode)currentRoot.Children[0];
        savedNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = savedNode.GetPath()
        });
        var bundle = TestTreeFactory.Bundle("DiskA", currentRoot);
        var service = new DeclarationSyncService(
            repoRoot,
            RepoNodeVM.Create(repoRoot),
            new List<FileDataVMBundle> { bundle });

        var scannedRoot = FileNode.CreateByPathSkippingDeclaredSubtrees(
            tempFolder.Path,
            currentRoot,
            progress: null,
            CancellationToken.None);

        Assert.NotNull(scannedRoot);
        var refreshedRoot = service.BuildRefreshedFileNodeSubtree(
            currentRoot,
            scannedRoot);
        var failures = service.GetInvalidDeclareHoldingsAfterRefresh(
            "DiskA",
            currentRoot,
            refreshedRoot);

        var failure = Assert.Single(failures);
        Assert.Equal(savedNode.GetPath(), failure.FileNodePath);
        Assert.Equal(repoNode.GetPath(), failure.RepoNodePath);
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
