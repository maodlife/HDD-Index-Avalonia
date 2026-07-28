using HDD_Index.Application.FileScanning;
using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class FileTreeScannerTests
{
    [Fact]
    public void Scan_WithoutProgressBuildsExistingTree()
    {
        using var tempFolder = new TempFolder();
        File.WriteAllText(Path.Combine(tempFolder.Path, "file.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(tempFolder.Path, "folder"));
        var scanner = new FileTreeScanner();

        var result = scanner.Scan(
            new FileTreeScanRequest(tempFolder.Path),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Succeeded, result.Status);
        var root = Assert.IsType<FileNode>(result.Root);
        Assert.True(root.IsDirectory);
        Assert.Equal(Path.GetFileName(tempFolder.Path), root.Name);
        Assert.Contains(root.Children.OfType<FileNode>(), x => x.Name == "file.txt");
        Assert.Contains(root.Children.OfType<FileNode>(), x => x.Name == "folder");
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Scan_ReportsTopLevelVisibleEntryProgress()
    {
        using var tempFolder = new TempFolder();
        File.WriteAllText(Path.Combine(tempFolder.Path, "a.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.Path, "b.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.Path, ".hidden"), string.Empty);
        var childFolder = Directory.CreateDirectory(Path.Combine(tempFolder.Path, "folder"));
        File.WriteAllText(Path.Combine(childFolder.FullName, "nested.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(tempFolder.Path, ".hidden-folder"));
        var progress = new CapturingProgress();
        var scanner = new FileTreeScanner();

        var result = scanner.Scan(
            new FileTreeScanRequest(tempFolder.Path),
            progress,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Succeeded, result.Status);
        var root = Assert.IsType<FileNode>(result.Root);
        Assert.Equal(3, progress.Items.Max(x => x.TotalTopLevelEntries));
        Assert.Equal(3, progress.Items.Max(x => x.CompletedTopLevelEntries));
        Assert.DoesNotContain(root.Children.OfType<FileNode>(), x => x.Name == ".hidden");
        Assert.DoesNotContain(
            root.Children.OfType<FileNode>(),
            x => x.Name == ".hidden-folder");
    }

    [Fact]
    public void Scan_ReturnsCancelledWhenCancelledDuringScan()
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
        var scanner = new FileTreeScanner();

        var result = scanner.Scan(
            new FileTreeScanRequest(tempFolder.Path),
            progress,
            cancellationTokenSource.Token);

        Assert.Equal(FileTreeScanStatus.Cancelled, result.Status);
        Assert.Null(result.Root);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Scan_SkippingDeclaredSubtreesPreservesDeclaredChildSubtree()
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
        var scanner = new FileTreeScanner();

        var result = scanner.Scan(
            new FileTreeScanRequest(
                tempFolder.Path,
                currentRoot,
                SkipDeclaredSubtrees: true),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Succeeded, result.Status);
        var scannedRoot = Assert.IsType<FileNode>(result.Root);
        var scannedSaved = Assert.Single(
            scannedRoot.Children.OfType<FileNode>(),
            x => x.Name == "Saved");
        Assert.Single(scannedSaved.DeclareRepoNodeDatas);
        Assert.Contains(scannedSaved.Children.OfType<FileNode>(), x => x.Name == "old.txt");
        Assert.DoesNotContain(
            scannedSaved.Children.OfType<FileNode>(),
            x => x.Name == "new.txt");

        var scannedFresh = Assert.Single(
            scannedRoot.Children.OfType<FileNode>(),
            x => x.Name == "Fresh");
        Assert.Contains(scannedFresh.Children.OfType<FileNode>(), x => x.Name == "fresh.txt");
    }

    [Fact]
    public void Scan_WithoutSkipOptionRescansDeclaredChildSubtree()
    {
        using var tempFolder = new TempFolder();
        var savedFolderPath = Directory.CreateDirectory(
            Path.Combine(tempFolder.Path, "Saved"));
        File.WriteAllText(Path.Combine(savedFolderPath.FullName, "new.txt"), string.Empty);
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
        var scanner = new FileTreeScanner();

        var result = scanner.Scan(
            new FileTreeScanRequest(
                tempFolder.Path,
                currentRoot,
                SkipDeclaredSubtrees: false),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Succeeded, result.Status);
        var scannedRoot = Assert.IsType<FileNode>(result.Root);
        var scannedSaved = Assert.Single(
            scannedRoot.Children.OfType<FileNode>(),
            x => x.Name == "Saved");
        Assert.Empty(scannedSaved.DeclareRepoNodeDatas);
        Assert.Contains(scannedSaved.Children.OfType<FileNode>(), x => x.Name == "new.txt");
        Assert.DoesNotContain(
            scannedSaved.Children.OfType<FileNode>(),
            x => x.Name == "old.txt");
    }

    [Fact]
    public void Scan_SkippingDeclaredSubtreesReportsMissingDeclaredChildDuringRefresh()
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
        var declarationService = new DeclarationSyncService(
            repoRoot,
            new List<FileData> { bundle });
        var scanner = new FileTreeScanner();

        var result = scanner.Scan(
            new FileTreeScanRequest(
                tempFolder.Path,
                currentRoot,
                SkipDeclaredSubtrees: true),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Succeeded, result.Status);
        var scannedRoot = Assert.IsType<FileNode>(result.Root);
        var refreshedRoot = declarationService.BuildRefreshedFileNodeSubtree(
            currentRoot,
            scannedRoot);
        var failures = declarationService.GetInvalidDeclareHoldingsAfterRefresh(
            "DiskA",
            currentRoot,
            refreshedRoot);

        var failure = Assert.Single(failures);
        Assert.Equal(savedNode.GetPath(), failure.FileNodePath);
        Assert.Equal(repoNode.GetPath(), failure.RepoNodePath);
    }

    [Fact]
    public void Scan_RootAccessDeniedReturnsFailure()
    {
        const string rootPath = "root";
        var fileSystem = new StubFileSystemReader
        {
            EnumerateFilesHandler = path => path == rootPath
                ? throw new UnauthorizedAccessException("denied")
                : Array.Empty<string>()
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Failed, result.Status);
        Assert.Null(result.Root);
        var issue = Assert.Single(result.BlockingIssues);
        Assert.Equal(rootPath, issue.Path);
        Assert.Equal(FileTreeScanIssueKind.AccessDenied, issue.Kind);
    }

    [Fact]
    public void Scan_ChildAccessDeniedRejectsPartialTree()
    {
        const string rootPath = "root";
        var blockedPath = Path.Combine(rootPath, "blocked");
        var fileSystem = new StubFileSystemReader
        {
            EnumerateDirectoriesHandler = path => path == rootPath
                ? new[] { blockedPath }
                : Array.Empty<string>(),
            EnumerateFilesHandler = path => path == blockedPath
                ? throw new UnauthorizedAccessException("denied")
                : Array.Empty<string>()
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.PartiallyFailed, result.Status);
        Assert.Null(result.Root);
        var issue = Assert.Single(result.BlockingIssues);
        Assert.Equal(blockedPath, issue.Path);
        Assert.Equal(FileTreeScanIssueKind.AccessDenied, issue.Kind);
    }

    [Fact]
    public void Scan_ChildIoErrorRejectsPartialTree()
    {
        const string rootPath = "root";
        var blockedPath = Path.Combine(rootPath, "blocked");
        var fileSystem = new StubFileSystemReader
        {
            EnumerateDirectoriesHandler = path => path == rootPath
                ? new[] { blockedPath }
                : Array.Empty<string>(),
            EnumerateFilesHandler = path => path == blockedPath
                ? throw new IOException("disk failure")
                : Array.Empty<string>()
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.PartiallyFailed, result.Status);
        Assert.Null(result.Root);
        var issue = Assert.Single(result.BlockingIssues);
        Assert.Equal(blockedPath, issue.Path);
        Assert.Equal(FileTreeScanIssueKind.IoError, issue.Kind);
    }

    [Fact]
    public void Scan_AttributeReadFailureReturnsSuccessfulTreeWithWarning()
    {
        const string rootPath = "root";
        var filePath = Path.Combine(rootPath, "file.txt");
        var fileSystem = new StubFileSystemReader
        {
            EnumerateFilesHandler = path => path == rootPath
                ? new[] { filePath }
                : Array.Empty<string>(),
            GetAttributesHandler = path => path == filePath
                ? throw new IOException("attribute failure")
                : FileAttributes.Directory
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Succeeded, result.Status);
        var root = Assert.IsType<FileNode>(result.Root);
        Assert.Contains(root.Children.OfType<FileNode>(), x => x.Name == "file.txt");
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(filePath, warning.Path);
        Assert.Equal(FileTreeScanIssueKind.AttributeReadFailed, warning.Kind);
    }

    [Fact]
    public void Scan_RepeatedAttributeWarningIsDeduplicated()
    {
        const string rootPath = "root";
        var childPath = Path.Combine(rootPath, "child");
        var fileSystem = new StubFileSystemReader
        {
            EnumerateDirectoriesHandler = path => path == rootPath
                ? new[] { childPath }
                : Array.Empty<string>(),
            GetAttributesHandler = path => path == childPath
                ? throw new IOException("attribute failure")
                : FileAttributes.Directory
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Succeeded, result.Status);
        Assert.NotNull(result.Root);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(childPath, warning.Path);
    }

    [Fact]
    public void Scan_UnexpectedAttributeFailureRejectsPartialTree()
    {
        const string rootPath = "root";
        var filePath = Path.Combine(rootPath, "file.txt");
        var fileSystem = new StubFileSystemReader
        {
            EnumerateFilesHandler = path => path == rootPath
                ? new[] { filePath }
                : Array.Empty<string>(),
            GetAttributesHandler = path => path == filePath
                ? throw new InvalidOperationException("unexpected")
                : FileAttributes.Directory
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.PartiallyFailed, result.Status);
        Assert.Null(result.Root);
        var issue = Assert.Single(result.BlockingIssues);
        Assert.Equal(filePath, issue.Path);
        Assert.Equal(FileTreeScanIssueKind.Unexpected, issue.Kind);
    }

    [Fact]
    public void Scan_DirectoryReparsePointRejectsPartialTree()
    {
        const string rootPath = "root";
        var linkPath = Path.Combine(rootPath, "link");
        var fileSystem = new StubFileSystemReader
        {
            EnumerateDirectoriesHandler = path => path == rootPath
                ? new[] { linkPath }
                : Array.Empty<string>(),
            GetAttributesHandler = path => path == linkPath
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : FileAttributes.Directory
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.PartiallyFailed, result.Status);
        Assert.Null(result.Root);
        var issue = Assert.Single(result.BlockingIssues);
        Assert.Equal(linkPath, issue.Path);
        Assert.Equal(FileTreeScanIssueKind.DirectoryReparsePoint, issue.Kind);
    }

    [Fact]
    public void Scan_RootDirectoryReparsePointReturnsFailure()
    {
        const string rootPath = "root-link";
        var fileSystem = new StubFileSystemReader
        {
            GetAttributesHandler = path => path == rootPath
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : FileAttributes.Directory
        };
        var scanner = new FileTreeScanner(fileSystem);

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Failed, result.Status);
        Assert.Null(result.Root);
        var issue = Assert.Single(result.BlockingIssues);
        Assert.Equal(rootPath, issue.Path);
        Assert.Equal(FileTreeScanIssueKind.DirectoryReparsePoint, issue.Kind);
    }

    [Fact]
    public void Scan_HiddenRootReturnsFailure()
    {
        const string rootPath = ".hidden-root";
        var scanner = new FileTreeScanner(new StubFileSystemReader());

        var result = scanner.Scan(
            new FileTreeScanRequest(rootPath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(FileTreeScanStatus.Failed, result.Status);
        Assert.Null(result.Root);
        var issue = Assert.Single(result.BlockingIssues);
        Assert.Equal(rootPath, issue.Path);
        Assert.Equal(FileTreeScanIssueKind.HiddenRoot, issue.Kind);
    }

    private sealed class CapturingProgress : IProgress<FileTreeScanProgress>
    {
        private readonly Action<FileTreeScanProgress>? _onReport;

        public CapturingProgress(Action<FileTreeScanProgress>? onReport = null)
        {
            _onReport = onReport;
        }

        public List<FileTreeScanProgress> Items { get; } = new();

        public void Report(FileTreeScanProgress value)
        {
            Items.Add(value);
            _onReport?.Invoke(value);
        }
    }

    private sealed class StubFileSystemReader : IFileSystemReader
    {
        public Func<string, IEnumerable<string>> EnumerateFilesHandler { get; init; } =
            _ => Array.Empty<string>();

        public Func<string, IEnumerable<string>> EnumerateDirectoriesHandler { get; init; } =
            _ => Array.Empty<string>();

        public Func<string, FileAttributes> GetAttributesHandler { get; init; } =
            _ => FileAttributes.Directory;

        public IEnumerable<string> EnumerateFiles(string path)
        {
            return EnumerateFilesHandler(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return EnumerateDirectoriesHandler(path);
        }

        public FileAttributes GetAttributes(string path)
        {
            return GetAttributesHandler(path);
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
