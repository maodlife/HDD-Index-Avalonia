using HDD_Index.Models;
using HDD_Index.Services;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

// 这个文件测试 DeclarationSyncService 维护“仓库树 RepoNode”和“文件树 FileNode”之间声明关系的规则。
// 测试里的 Repo 树表示用户整理出来的目录结构，File 树表示某个磁盘里的真实文件结构。
// DeclareRepoNodeData 表示“文件节点声明自己对应某个仓库节点”，SaveFileNodeData 表示“仓库节点记录自己保存在哪个磁盘文件节点”。
public class DeclarationSyncServiceTests
{
    // 场景：
    // 仓库树是 Root -> Movies，文件树是 Disk -> Movies；
    // 文件树的 Movies 节点已经声明自己对应仓库树的 Movies 节点。
    //
    // 期望：
    // CheckRepoNodeAndFileNodeIsSync 返回 true，说明这两个节点被认为是同步匹配的。
    [Fact]
    public void CheckRepoNodeAndFileNodeIsSync_ReturnsTrueWhenFileDeclaresSelectedRepo()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });

        var service = new DeclarationSyncService(
            repoRoot,
            RepoNodeVM.Create(repoRoot),
            new List<FileDataVMBundle> { TestTreeFactory.Bundle("DiskA", fileRoot) });

        Assert.True(service.CheckRepoNodeAndFileNodeIsSync(repoNode, fileNode));
    }

    [Fact]
    public void TryDeclareHolding_AddsModelAndVmDeclarationData()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.RepoFile("movie.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                TestTreeFactory.DiskFile("movie.mkv")));
        var fileNode = (FileNode)fileRoot.Children[0];
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var repoNodeVm = (RepoNodeVM)repoRootVm.Children[0];
        var fileNodeVm = (FileNodeVM)bundle.FileNodeVm.Children[0];
        var propertyChangedNames = new List<string?>();
        repoNodeVm.PropertyChanged += (_, e) => propertyChangedNames.Add(e.PropertyName);
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });

        var result = service.TryDeclareHolding(
            repoNode,
            repoNodeVm,
            fileNode,
            fileNodeVm,
            "DiskA",
            DeclareHoldingStrategyType.Default,
            saveStrategyToRepoNode: true,
            out var failureReason);

        Assert.True(result);
        Assert.Empty(failureReason);
        Assert.Equal(DeclareHoldingStrategyType.Default, repoNode.DeclareHoldingStrategyType);
        Assert.Equal("默认", repoNodeVm.DeclareHoldingStrategyName);
        Assert.Contains(nameof(RepoNodeVM.DeclareHoldingStrategyName), propertyChangedNames);
        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(repoNodeVm.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
        Assert.Single(fileNodeVm.DeclareRepoNodeDatas);
        Assert.Equal(fileNode.GetPath(), repoNode.SaveFileNodeDatas[0].FileNodePath);
        Assert.Equal(repoNode.GetPath(), fileNode.DeclareRepoNodeDatas[0].RepoNodePath);
    }

    [Fact]
    public void TryDeclareHolding_DoesNotAddDuplicateDeclarationData()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var repoNodeVm = (RepoNodeVM)repoRootVm.Children[0];
        var fileNodeVm = (FileNodeVM)bundle.FileNodeVm.Children[0];
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });

        service.TryDeclareHolding(
            repoNode,
            repoNodeVm,
            fileNode,
            fileNodeVm,
            "DiskA",
            DeclareHoldingStrategyType.Default,
            saveStrategyToRepoNode: true,
            out _);
        service.TryDeclareHolding(
            repoNode,
            repoNodeVm,
            fileNode,
            fileNodeVm,
            "DiskA",
            DeclareHoldingStrategyType.Default,
            saveStrategyToRepoNode: false,
            out _);

        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(repoNodeVm.SaveFileNodeDatas);
        Assert.Single(fileNode.DeclareRepoNodeDatas);
        Assert.Single(fileNodeVm.DeclareRepoNodeDatas);
    }

    [Fact]
    public void TryDeclareHolding_ReturnsFalseAndDoesNotSaveStrategyWhenValidationFails()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.RepoFile("movie.mkv")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var repoNodeVm = (RepoNodeVM)repoRootVm.Children[0];
        var fileNodeVm = (FileNodeVM)bundle.FileNodeVm.Children[0];
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });

        var result = service.TryDeclareHolding(
            repoNode,
            repoNodeVm,
            fileNode,
            fileNodeVm,
            "DiskA",
            DeclareHoldingStrategyType.Default,
            saveStrategyToRepoNode: true,
            out var failureReason);

        Assert.False(result);
        Assert.Contains("movie.mkv", failureReason);
        Assert.Null(repoNode.DeclareHoldingStrategyType);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(repoNodeVm.SaveFileNodeDatas);
        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(fileNodeVm.DeclareRepoNodeDatas);
    }

    [Fact]
    public void ApplyDeclareHoldingStrategy_UpdatesStrategyWhenNodeHasNoSaveData()
    {
        var repoRoot = TestTreeFactory.Repo("Root", TestTreeFactory.Repo("Movies"));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var repoNodeVm = (RepoNodeVM)repoRootVm.Children[0];
        var propertyChangedNames = new List<string?>();
        repoNodeVm.PropertyChanged += (_, e) => propertyChangedNames.Add(e.PropertyName);
        var fileRoot = TestTreeFactory.File("Disk");
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { TestTreeFactory.Bundle("DiskA", fileRoot) });

        var failures = service.GetInvalidSaveFileNodeDatasForStrategy(
            repoNode,
            DeclareHoldingStrategyType.BDRip);
        service.ApplyDeclareHoldingStrategy(
            repoNode,
            DeclareHoldingStrategyType.BDRip,
            failures);

        Assert.Empty(failures);
        Assert.Equal(DeclareHoldingStrategyType.BDRip, repoNode.DeclareHoldingStrategyType);
        Assert.Equal("BDRip", repoNodeVm.DeclareHoldingStrategyName);
        Assert.Contains(nameof(RepoNodeVM.DeclareHoldingStrategyName), propertyChangedNames);
    }

    [Fact]
    public void GetInvalidSaveFileNodeDatasForStrategy_DoesNotMutateWhenCallerCancels()
    {
        var (repoNode, repoNodeVm, invalidFileNode, invalidFileNodeVm, _, service) =
            CreateStrategyChangeScenario();

        var failures = service.GetInvalidSaveFileNodeDatasForStrategy(
            repoNode,
            DeclareHoldingStrategyType.Default);

        Assert.Single(failures);
        Assert.Equal(DeclareHoldingStrategyType.BDRip, repoNode.DeclareHoldingStrategyType);
        Assert.Equal("BDRip", repoNodeVm.DeclareHoldingStrategyName);
        Assert.Equal(2, repoNode.SaveFileNodeDatas.Count);
        Assert.Single(invalidFileNode.DeclareRepoNodeDatas);
        Assert.Single(invalidFileNodeVm.DeclareRepoNodeDatas);
    }

    [Fact]
    public void ApplyDeclareHoldingStrategy_RemovesInvalidBidirectionalDeclarationData()
    {
        var (
            repoNode,
            repoNodeVm,
            invalidFileNode,
            invalidFileNodeVm,
            validFileNode,
            service) = CreateStrategyChangeScenario();

        var failures = service.GetInvalidSaveFileNodeDatasForStrategy(
            repoNode,
            DeclareHoldingStrategyType.Default);
        service.ApplyDeclareHoldingStrategy(
            repoNode,
            DeclareHoldingStrategyType.Default,
            failures);

        Assert.Single(failures);
        Assert.Equal(DeclareHoldingStrategyType.Default, repoNode.DeclareHoldingStrategyType);
        Assert.Equal("默认", repoNodeVm.DeclareHoldingStrategyName);
        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(repoNodeVm.SaveFileNodeDatas);
        Assert.Equal(validFileNode.GetPath(), repoNode.SaveFileNodeDatas[0].FileNodePath);
        Assert.Empty(invalidFileNode.DeclareRepoNodeDatas);
        Assert.Empty(invalidFileNodeVm.DeclareRepoNodeDatas);
        Assert.Single(validFileNode.DeclareRepoNodeDatas);
    }

    [Fact]
    public void ApplyDeclareHoldingStrategy_ClearsStrategyAfterDefaultValidation()
    {
        var (
            repoNode,
            repoNodeVm,
            invalidFileNode,
            invalidFileNodeVm,
            validFileNode,
            service) = CreateStrategyChangeScenario();

        var failures = service.GetInvalidSaveFileNodeDatasForStrategy(
            repoNode,
            strategyType: null);
        service.ApplyDeclareHoldingStrategy(
            repoNode,
            strategyType: null,
            failures);

        Assert.Single(failures);
        Assert.Null(repoNode.DeclareHoldingStrategyType);
        Assert.Empty(repoNodeVm.DeclareHoldingStrategyName);
        Assert.Single(repoNode.SaveFileNodeDatas);
        Assert.Single(repoNodeVm.SaveFileNodeDatas);
        Assert.Equal(validFileNode.GetPath(), repoNode.SaveFileNodeDatas[0].FileNodePath);
        Assert.Empty(invalidFileNode.DeclareRepoNodeDatas);
        Assert.Empty(invalidFileNodeVm.DeclareRepoNodeDatas);
        Assert.Single(validFileNode.DeclareRepoNodeDatas);
    }

    [Fact]
    public void AbandonDeclareHoldings_RemovesSelectedBidirectionalDeclarationData()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies"),
            TestTreeFactory.Repo("Music"));
        var moviesRepoNode = (RepoNode)repoRoot.Children[0];
        var musicRepoNode = (RepoNode)repoRoot.Children[1];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Media"));
        var fileNode = (FileNode)fileRoot.Children[0];
        var fileNodePath = fileNode.GetPath();
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = moviesRepoNode.GetPath()
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = musicRepoNode.GetPath()
        });
        moviesRepoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNodePath
        });
        musicRepoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNodePath
        });
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var fileNodeVm = (FileNodeVM)bundle.FileNodeVm.Children[0];
        var moviesRepoNodeVm = (RepoNodeVM)repoRootVm.Children[0];
        var musicRepoNodeVm = (RepoNodeVM)repoRootVm.Children[1];
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });

        service.AbandonDeclareHoldings(
            fileNode,
            "DiskA",
            new[] { moviesRepoNode.GetPath() });

        Assert.DoesNotContain(fileNode.DeclareRepoNodeDatas,
            x => x.RepoNodePath == moviesRepoNode.GetPath());
        Assert.Contains(fileNode.DeclareRepoNodeDatas,
            x => x.RepoNodePath == musicRepoNode.GetPath());
        Assert.Empty(moviesRepoNode.SaveFileNodeDatas);
        Assert.Single(musicRepoNode.SaveFileNodeDatas);
        Assert.Empty(moviesRepoNodeVm.SaveFileNodeDatas);
        Assert.Single(musicRepoNodeVm.SaveFileNodeDatas);
        Assert.DoesNotContain(fileNodeVm.DeclareRepoNodeDatas,
            x => x.RepoNodePath == moviesRepoNode.GetPath());
        Assert.Contains(fileNodeVm.DeclareRepoNodeDatas,
            x => x.RepoNodePath == musicRepoNode.GetPath());
    }

    [Fact]
    public void AbandonDeclareHoldings_RemovesMultiplePathsAndIgnoresUnknownPaths()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo("Movies"),
            TestTreeFactory.Repo("Music"));
        var moviesRepoNode = (RepoNode)repoRoot.Children[0];
        var musicRepoNode = (RepoNode)repoRoot.Children[1];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Media"));
        var fileNode = (FileNode)fileRoot.Children[0];
        var fileNodePath = fileNode.GetPath();
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = moviesRepoNode.GetPath()
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = musicRepoNode.GetPath()
        });
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Root/Missing"
        });
        moviesRepoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNodePath
        });
        musicRepoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNodePath
        });
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var fileNodeVm = (FileNodeVM)bundle.FileNodeVm.Children[0];
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });

        service.AbandonDeclareHoldings(
            fileNode,
            "DiskA",
            new[] { moviesRepoNode.GetPath(), musicRepoNode.GetPath(), "Root/Missing" });

        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(fileNodeVm.DeclareRepoNodeDatas);
        Assert.Empty(moviesRepoNode.SaveFileNodeDatas);
        Assert.Empty(musicRepoNode.SaveFileNodeDatas);
        Assert.Empty(((RepoNodeVM)repoRootVm.Children[0]).SaveFileNodeDatas);
        Assert.Empty(((RepoNodeVM)repoRootVm.Children[1]).SaveFileNodeDatas);
    }

    // 场景：
    // 仓库树是 Root -> Movies -> Anime，但文件树只有 Disk -> Movies；
    // 文件树 Movies 节点声明了对应仓库树 Movies，仓库树 Movies 也保存了一条 DiskA 的文件节点记录。
    //
    // 期望：
    // 因为文件树 Movies 下面缺少 Anime，当前文件节点已经不能完整匹配仓库子树；
    // 更新受影响声明后，文件节点声明、仓库节点保存记录、FileNodeVM 上的声明副本都应该被移除。
    [Fact]
    public void UpdateAffectedFileNodesDeclaration_RemovesDeclarationWhenRepoSubtreeNoLongerMatches()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.Repo("Anime")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var fileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var fileNode = (FileNode)fileRoot.Children[0];
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = fileNode.GetPath()
        });
        var bundle = TestTreeFactory.Bundle("DiskA", fileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { bundle });

        var affectedNodes = service.GetAffectedFileNodes(repoNode, includeDescendants: false);
        service.UpdateAffectedFileNodesDeclaration(affectedNodes);

        Assert.Empty(fileNode.DeclareRepoNodeDatas);
        Assert.Empty(repoNode.SaveFileNodeDatas);
        Assert.Empty(((FileNodeVM)bundle.FileNodeVm.Children[0]).DeclareRepoNodeDatas);
    }

    // 场景：
    // 同一个仓库节点 Movies 同时保存到两个磁盘：
    // DiskA 的文件树只有 Movies，缺少 Anime，因此是无效匹配；
    // DiskB 的文件树有 Movies -> Anime，因此仍然是有效匹配。
    //
    // 期望：
    // 更新声明时只移除 DiskA 那一份无效关系；
    // DiskB 上的声明和仓库节点中的 DiskB 保存记录都要保留。
    [Fact]
    public void UpdateAffectedFileNodesDeclaration_RemovesOnlyAffectedDiskWhenFilePathsMatch()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.Repo("Anime")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        var invalidFileRoot = TestTreeFactory.File("Disk", TestTreeFactory.File("Movies"));
        var invalidFileNode = (FileNode)invalidFileRoot.Children[0];
        var validFileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                TestTreeFactory.File("Anime")));
        var validFileNode = (FileNode)validFileRoot.Children[0];
        invalidFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        validFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskB",
            FileNodePath = validFileNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = invalidFileNode.GetPath()
        });
        var invalidBundle = TestTreeFactory.Bundle("DiskA", invalidFileRoot);
        var validBundle = TestTreeFactory.Bundle("DiskB", validFileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { invalidBundle, validBundle });

        var affectedNodes = service.GetAffectedFileNodes(repoNode, includeDescendants: false);
        service.UpdateAffectedFileNodesDeclaration(affectedNodes);

        Assert.Empty(invalidFileNode.DeclareRepoNodeDatas);
        Assert.Single(validFileNode.DeclareRepoNodeDatas);
        Assert.DoesNotContain(repoNode.SaveFileNodeDatas, x => x.DiskLabel == "DiskA");
        Assert.Contains(repoNode.SaveFileNodeDatas, x => x.DiskLabel == "DiskB");
        Assert.Empty(invalidBundle.FileNodeVm.Children[0].DeclareRepoNodeDatas);
        Assert.Single(validBundle.FileNodeVm.Children[0].DeclareRepoNodeDatas);
    }

    private static (
        RepoNode RepoNode,
        RepoNodeVM RepoNodeVm,
        FileNode InvalidFileNode,
        FileNodeVM InvalidFileNodeVm,
        FileNode ValidFileNode,
        DeclarationSyncService Service) CreateStrategyChangeScenario()
    {
        var repoRoot = TestTreeFactory.Repo(
            "Root",
            TestTreeFactory.Repo(
                "Movies",
                TestTreeFactory.RepoFile("movie.mkv"),
                TestTreeFactory.RepoFile("subtitle.ass")));
        var repoNode = (RepoNode)repoRoot.Children[0];
        repoNode.DeclareHoldingStrategyType = DeclareHoldingStrategyType.BDRip;

        var invalidFileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                TestTreeFactory.DiskFile("movie.mkv")));
        var invalidFileNode = (FileNode)invalidFileRoot.Children[0];
        var validFileRoot = TestTreeFactory.File(
            "Disk",
            TestTreeFactory.File(
                "Movies",
                TestTreeFactory.DiskFile("movie.mkv"),
                TestTreeFactory.DiskFile("subtitle.ass")));
        var validFileNode = (FileNode)validFileRoot.Children[0];

        invalidFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        validFileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = repoNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = invalidFileNode.GetPath()
        });
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskB",
            FileNodePath = validFileNode.GetPath()
        });

        var invalidBundle = TestTreeFactory.Bundle("DiskA", invalidFileRoot);
        var validBundle = TestTreeFactory.Bundle("DiskB", validFileRoot);
        var repoRootVm = RepoNodeVM.Create(repoRoot);
        var service = new DeclarationSyncService(
            repoRoot,
            repoRootVm,
            new List<FileDataVMBundle> { invalidBundle, validBundle });

        return (
            repoNode,
            (RepoNodeVM)repoRootVm.Children[0],
            invalidFileNode,
            (FileNodeVM)invalidBundle.FileNodeVm.Children[0],
            validFileNode,
            service);
    }
}
