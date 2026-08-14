using HDD_Index.Application.FileScanning;
using HDD_Index.Application.FileTrees;
using HDD_Index.Application.Persistence;
using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class FileTreeUseCasesTests
{
    [Fact]
    public void NewFileTreePlanValidatesInputWithoutMutatingSession()
    {
        var existing = TestTreeFactory.Bundle(
            "DiskA",
            TestTreeFactory.File("DiskA"));
        var (useCases, session, _, pathService) = CreateUseCases(existing);

        var missingInput = useCases.PlanNewFileTree("  ", "  ");
        var duplicate = useCases.PlanNewFileTree("C:\\Data", " diska ");
        pathService.InvalidFileNames.Add("Bad:Disk");
        var invalidName = useCases.PlanNewFileTree("C:\\Data", "Bad:Disk");
        pathService.ExistingFiles.Add("C:\\Index\\DiskB.json");
        var existingJson = useCases.PlanNewFileTree("C:\\Data", "DiskB");

        Assert.False(missingInput.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.False(invalidName.Succeeded);
        Assert.False(existingJson.Succeeded);
        Assert.Single(session.FileDatas);
        Assert.Empty(session.AppConfig.FileDataFiles);
    }

    [Fact]
    public void NewFileTreeIsAddedOnlyAfterSuccessfulScan()
    {
        var scannedRoot = TestTreeFactory.File(
            "Data",
            TestTreeFactory.DiskFile("movie.mkv"));
        var scanner = new StubFileTreeScanner(
            FileTreeScanResult.Success(scannedRoot, []));
        var (useCases, session, _, _) = CreateUseCases(scanner: scanner);
        var plan = useCases.PlanNewFileTree(" C:\\Data ", " DiskA ");

        var scanResult = useCases.ScanNewFileTree(
            plan,
            progress: null,
            CancellationToken.None);

        Assert.True(plan.Succeeded);
        Assert.Empty(session.FileDatas);
        Assert.Empty(session.AppConfig.FileDataFiles);
        Assert.Equal("C:\\Data", scanner.LastRequest!.RootPath);

        var result = useCases.ApplyNewFileTree(plan, scanResult);

        Assert.True(result.Succeeded);
        Assert.Same(session.FileDatas.Single(), result.AddedFileData);
        Assert.Same(scannedRoot, result.AddedFileData!.FileNodeRoot);
        Assert.Equal("DiskA", result.AddedFileData.DiskLabel);
        Assert.Equal("C:\\Index\\DiskA.json", result.AddedFileData.JsonFilePath);
        Assert.Equal("DiskA.json", session.AppConfig.FileDataFiles.Single().JsonFilePath);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.AppConfig,
                PersistenceTarget.ForFileData("DiskA"),
            },
            result.PersistenceTargets);
    }

    [Fact]
    public void FailedNewFileTreeScanDoesNotMutateSession()
    {
        var scanResult = FileTreeScanResult.Failure(
            FileTreeScanStatus.Failed,
            [new FileTreeScanIssue(
                "C:\\Data",
                FileTreeScanIssueSeverity.Blocking,
                FileTreeScanIssueKind.AccessDenied,
                "无权限访问")]);
        var (useCases, session, _, _) = CreateUseCases();
        var plan = useCases.PlanNewFileTree("C:\\Data", "DiskA");

        var result = useCases.ApplyNewFileTree(plan, scanResult);

        Assert.False(result.Succeeded);
        Assert.Empty(result.PersistenceTargets);
        Assert.Empty(session.FileDatas);
        Assert.Empty(session.AppConfig.FileDataFiles);
    }

    [Fact]
    public void NewFileTreePlanRejectsConfiguredIndexThatWasNotLoaded()
    {
        var (useCases, session, _, _) = CreateUseCases();
        session.AppConfig.FileDataFiles.Add(new FileDataFileConfig
        {
            JsonFilePath = "Unavailable\\DiskA.json",
            LocalFolderPath = "D:\\Unavailable",
        });

        var plan = useCases.PlanNewFileTree("E:\\Data", "diska");

        Assert.False(plan.Succeeded);
        Assert.Contains("启动加载失败", plan.FailureReason);
        Assert.Empty(session.FileDatas);
    }

    [Fact]
    public void CreatingFirstConfiguredTreeMigratesLegacyFileDataEntries()
    {
        var legacy = TestTreeFactory.Bundle(
            "Legacy",
            TestTreeFactory.File("Legacy"));
        legacy.JsonFilePath = "C:\\Index\\Legacy.json";
        legacy.LocalFolderPath = "D:\\Legacy";
        var scannedRoot = TestTreeFactory.File("New");
        var (useCases, session, _, _) = CreateUseCases(legacy);
        var plan = useCases.PlanNewFileTree("E:\\New", "New");

        var result = useCases.ApplyNewFileTree(
            plan,
            FileTreeScanResult.Success(scannedRoot, []));

        Assert.True(result.Succeeded);
        Assert.Equal(2, session.AppConfig.FileDataFiles.Count);
        Assert.Equal(
            "Legacy.json",
            session.AppConfig.FileDataFiles[0].JsonFilePath);
        Assert.Equal(
            "New.json",
            session.AppConfig.FileDataFiles[1].JsonFilePath);
    }

    [Fact]
    public void LocalPathUsesFileDataRootAndNodeRelativeSegments()
    {
        var movie = TestTreeFactory.DiskFile("movie.mkv");
        var folder = TestTreeFactory.File("Movies", movie);
        var root = TestTreeFactory.File("Disk", folder);
        var fileData = TestTreeFactory.Bundle("DiskA", root);
        fileData.LocalFolderPath = "D:\\Archive";
        var (useCases, _, _, _) = CreateUseCases(fileData);

        Assert.Equal(
            "D:\\Archive",
            useCases.GetLocalPath(fileData, root));
        Assert.Equal(
            "D:\\Archive\\Movies\\movie.mkv",
            useCases.GetLocalPath(fileData, movie));
        Assert.Null(useCases.GetLocalPath(null, movie));
    }

    [Fact]
    public void RefreshPlanAndScanDoNotMutateUntilApplied()
    {
        var oldFile = TestTreeFactory.DiskFile("old.txt");
        var root = TestTreeFactory.File("Disk", oldFile);
        var fileData = TestTreeFactory.Bundle("DiskA", root);
        fileData.LocalFolderPath = "D:\\Disk";
        var scannedRoot = TestTreeFactory.File(
            "DifferentScannerName",
            TestTreeFactory.DiskFile("new.txt"));
        var scanner = new StubFileTreeScanner(
            FileTreeScanResult.Success(scannedRoot, []));
        var (useCases, _, _, _) = CreateUseCases(fileData, scanner: scanner);
        var plan = useCases.PlanRefresh(
            fileData,
            root,
            skipDeclaredSubtrees: true);

        var scanResult = useCases.ScanRefresh(
            plan,
            progress: null,
            CancellationToken.None);

        Assert.True(plan.Succeeded);
        Assert.Equal("D:\\Disk", plan.LocalPath);
        Assert.Single(root.Children);
        Assert.Equal("old.txt", root.Children.Single().Name);
        Assert.True(scanner.LastRequest!.SkipDeclaredSubtrees);
        Assert.Same(root, scanner.LastRequest.CurrentRoot);

        var result = useCases.ApplyRefresh(scanResult);

        Assert.True(result.Succeeded);
        Assert.Equal("Disk", root.Name);
        Assert.Equal("new.txt", root.Children.Single().Name);
        Assert.Contains(
            result.Changes.Changes,
            change => change is Application.TreeEditing.FileNodeSubtreeReplaced);
        Assert.Equal(
            new[] { PersistenceTarget.ForFileData("DiskA") },
            result.PersistenceTargets);
    }

    [Fact]
    public void RefreshWithInvalidDeclarationCleansBothSidesAndMarksRepository()
    {
        var repoNode = TestTreeFactory.Repo(
            "Movies",
            TestTreeFactory.RepoFile("movie.mkv"));
        var repoRoot = TestTreeFactory.Repo("Root", repoNode);
        var movieFile = TestTreeFactory.DiskFile("movie.mkv");
        var moviesFolder = TestTreeFactory.File("Movies", movieFile);
        var fileRoot = TestTreeFactory.File("Disk", moviesFolder);
        var fileData = TestTreeFactory.Bundle("DiskA", fileRoot);
        fileData.LocalFolderPath = "D:\\Disk";
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = moviesFolder.GetPath(),
        });
        moviesFolder.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath(),
        });
        var scanner = new StubFileTreeScanner(
            FileTreeScanResult.Success(TestTreeFactory.File("Movies"), []));
        var (useCases, _, _, _) = CreateUseCases(
            fileData,
            repoRoot: repoRoot,
            scanner: scanner);
        var plan = useCases.PlanRefresh(
            fileData,
            moviesFolder,
            skipDeclaredSubtrees: false);

        var scanResult = useCases.ScanRefresh(
            plan,
            progress: null,
            CancellationToken.None);

        Assert.Single(scanResult.ValidationFailures);
        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(moviesFolder.DeclareRepoNodeDatas);

        var result = useCases.ApplyRefresh(scanResult);

        Assert.True(result.Succeeded);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(moviesFolder.DeclareRepoNodeDatas);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
            },
            result.PersistenceTargets);
    }

    [Fact]
    public void FailedRefreshCannotBeAppliedAndDoesNotMutate()
    {
        var oldFile = TestTreeFactory.DiskFile("old.txt");
        var root = TestTreeFactory.File("Disk", oldFile);
        var fileData = TestTreeFactory.Bundle("DiskA", root);
        fileData.LocalFolderPath = "D:\\Disk";
        var scanner = new StubFileTreeScanner(FileTreeScanResult.Cancelled());
        var (useCases, _, _, _) = CreateUseCases(fileData, scanner: scanner);
        var plan = useCases.PlanRefresh(fileData, root, false);
        var scanResult = useCases.ScanRefresh(
            plan,
            progress: null,
            CancellationToken.None);

        var result = useCases.ApplyRefresh(scanResult);

        Assert.False(result.Succeeded);
        Assert.Empty(result.PersistenceTargets);
        Assert.Same(oldFile, root.Children.Single());
    }

    [Fact]
    public void DeleteReturnsRepositoryAndCurrentFileDataTargets()
    {
        var child = TestTreeFactory.DiskFile("movie.mkv");
        var root = TestTreeFactory.File("Disk", child);
        var fileData = TestTreeFactory.Bundle("DiskA", root);
        var (useCases, _, _, _) = CreateUseCases(fileData);

        var result = useCases.DeleteFileNode(fileData, child);

        Assert.True(result.Succeeded);
        Assert.Empty(root.Children);
        Assert.Equal(
            new[]
            {
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
            },
            result.PersistenceTargets);

        var rootResult = useCases.DeleteFileNode(fileData, root);
        Assert.False(rootResult.Succeeded);
        Assert.Empty(rootResult.PersistenceTargets);
    }

    [Fact]
    public void UpdateLocalFolderPathChangesConfigAndMarksOnlyConfigDirty()
    {
        var fileData = TestTreeFactory.Bundle(
            "DiskA",
            TestTreeFactory.File("DiskA"));
        fileData.JsonFilePath = "C:\\Index\\DiskA.json";
        fileData.LocalFolderPath = "D:\\Old";
        var (useCases, session, _, _) = CreateUseCases(fileData);

        var result = useCases.UpdateLocalFolderPath(fileData, " E:\\Moved ");

        Assert.True(result.Succeeded);
        Assert.Equal("E:\\Moved", fileData.LocalFolderPath);
        var config = Assert.Single(session.AppConfig.FileDataFiles);
        Assert.Equal("DiskA.json", config.JsonFilePath);
        Assert.Equal("E:\\Moved", config.LocalFolderPath);
        Assert.Equal(
            new[] { PersistenceTarget.AppConfig },
            result.PersistenceTargets);
    }

    private static (
        FileTreeUseCases UseCases,
        ApplicationSession Session,
        StubFileTreeScanner Scanner,
        StubFileTreePathService PathService) CreateUseCases(
            FileData? fileData = null,
            RepoNode? repoRoot = null,
            StubFileTreeScanner? scanner = null)
    {
        var fileDatas = fileData == null
            ? new List<FileData>()
            : new List<FileData> { fileData };
        var session = new ApplicationSession(
            "C:\\Index\\config.json",
            new AppConfig
            {
                JsonFilePath = "C:\\Index",
                RepoFileName = "Repository.json",
            },
            repoRoot ?? TestTreeFactory.Repo("Root"),
            fileDatas);
        scanner ??= new StubFileTreeScanner(
            FileTreeScanResult.Success(TestTreeFactory.File("Scanned"), []));
        var pathService = new StubFileTreePathService();
        var declarationService = new DeclarationSyncService(
            session.RepoNodeRoot,
            fileDatas);
        var editor = new FileTreeEditor(declarationService);
        return (
            new FileTreeUseCases(
                session,
                editor,
                scanner,
                pathService),
            session,
            scanner,
            pathService);
    }

    private sealed class StubFileTreeScanner : IFileTreeScanner
    {
        private readonly FileTreeScanResult _result;

        public StubFileTreeScanner(FileTreeScanResult result)
        {
            _result = result;
        }

        public FileTreeScanRequest? LastRequest { get; private set; }

        public FileTreeScanResult Scan(
            FileTreeScanRequest request,
            IProgress<FileTreeScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return _result;
        }
    }

    private sealed class StubFileTreePathService : IFileTreePathService
    {
        public HashSet<string> InvalidFileNames { get; } = [];

        public HashSet<string> ExistingFiles { get; } = [];

        public bool ContainsInvalidFileNameChars(string fileName)
        {
            return InvalidFileNames.Contains(fileName);
        }

        public bool FileExists(string path)
        {
            return ExistingFiles.Contains(path);
        }

        public string Combine(string firstPath, string secondPath)
        {
            return $"{firstPath.TrimEnd('\\')}\\{secondPath.TrimStart('\\')}";
        }

        public string GetRelativePath(string relativeTo, string path)
        {
            var prefix = relativeTo.TrimEnd('\\') + "\\";
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path[prefix.Length..]
                : path;
        }

        public string GetFileNameWithoutExtension(string path)
        {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }
}
