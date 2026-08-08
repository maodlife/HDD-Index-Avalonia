using HDD_Index.Application.Persistence;
using HDD_Index.Models;

namespace HDD_Index.Tests;

public class ApplicationSessionManagerTests
{
    [Fact]
    public void MarkDirty_TracksLogicalTargetsAndReturnsSortedFilePaths()
    {
        var session = CreateSession();
        var store = new RecordingSessionStore();
        var manager = new ApplicationSessionManager(session, store);

        manager.MarkDirty(
            PersistenceTarget.Repository,
            PersistenceTarget.ForFileData("DiskB"),
            PersistenceTarget.ForFileData("Missing"));

        Assert.True(manager.HasDirtyFiles);
        Assert.Equal(
            new[]
            {
                @"C:\data\disk-b.json",
                @"C:\data\repo.json",
            },
            manager.GetDirtyFilePaths());
    }

    [Fact]
    public void SaveDirtyFiles_UsesStableOrderAndClearsTargetsAfterSuccess()
    {
        var session = CreateSession();
        var store = new RecordingSessionStore();
        var manager = new ApplicationSessionManager(session, store);
        manager.MarkDirty(
            PersistenceTarget.ForFileData("DiskB"),
            PersistenceTarget.Repository,
            PersistenceTarget.AppConfig,
            PersistenceTarget.ForFileData("DiskA"));

        manager.SaveDirtyFiles();

        Assert.Equal(
            new[]
            {
                PersistenceTarget.AppConfig,
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData("DiskA"),
                PersistenceTarget.ForFileData("DiskB"),
            },
            store.SavedTargets);
        Assert.False(manager.HasDirtyFiles);
        Assert.Empty(manager.GetDirtyFilePaths());
    }

    [Fact]
    public void SaveDirtyFiles_KeepsWholeBatchDirtyWhenOneSaveFails()
    {
        var session = CreateSession();
        var store = new RecordingSessionStore
        {
            TargetThatFails = PersistenceTarget.ForFileData("DiskA"),
        };
        var manager = new ApplicationSessionManager(session, store);
        manager.MarkDirty(
            PersistenceTarget.AppConfig,
            PersistenceTarget.Repository,
            PersistenceTarget.ForFileData("DiskA"),
            PersistenceTarget.ForFileData("DiskB"));

        Assert.Throws<IOException>(() => manager.SaveDirtyFiles());

        Assert.True(manager.HasDirtyFiles);
        Assert.Equal(4, manager.GetDirtyFilePaths().Count);

        store.TargetThatFails = null;
        manager.SaveDirtyFiles();

        Assert.False(manager.HasDirtyFiles);
        Assert.Equal(
            2,
            store.SavedTargets.Count(target =>
                target == PersistenceTarget.AppConfig));
        Assert.Equal(
            2,
            store.SavedTargets.Count(target =>
                target == PersistenceTarget.Repository));
    }

    [Fact]
    public void MarkDirty_ResolvesFileDataAddedAfterManagerCreation()
    {
        var session = CreateSession(fileDatas: []);
        var store = new RecordingSessionStore();
        var manager = new ApplicationSessionManager(session, store);
        session.FileDatas.Add(CreateFileData("DiskC", @"C:\data\disk-c.json"));

        manager.MarkDirty(PersistenceTarget.ForFileData("DiskC"));

        Assert.Equal(
            new[] { @"C:\data\disk-c.json" },
            manager.GetDirtyFilePaths());
    }

    [Fact]
    public void MarkAllFileDataDirty_DoesNotMarkConfigOrRepository()
    {
        var session = CreateSession();
        var store = new RecordingSessionStore();
        var manager = new ApplicationSessionManager(session, store);

        manager.MarkAllFileDataDirty();
        manager.SaveDirtyFiles();

        Assert.Equal(
            new[]
            {
                PersistenceTarget.ForFileData("DiskA"),
                PersistenceTarget.ForFileData("DiskB"),
            },
            store.SavedTargets);
    }

    private static ApplicationSession CreateSession(
        List<FileData>? fileDatas = null)
    {
        var appConfig = new AppConfig
        {
            JsonFilePath = @"C:\data",
            RepoFileName = "repo.json",
        };
        return new ApplicationSession(
            @"C:\data\config.json",
            appConfig,
            TestTreeFactory.Repo("Repo"),
            fileDatas
            ??
            [
                CreateFileData("DiskA", @"C:\data\disk-a.json"),
                CreateFileData("DiskB", @"C:\data\disk-b.json"),
            ]);
    }

    private static FileData CreateFileData(
        string diskLabel,
        string jsonFilePath)
    {
        return new FileData
        {
            DiskLabel = diskLabel,
            JsonFilePath = jsonFilePath,
            FileNodeRoot = TestTreeFactory.File(diskLabel),
        };
    }

    private sealed class RecordingSessionStore : IApplicationSessionStore
    {
        public List<PersistenceTarget> SavedTargets { get; } = [];

        public PersistenceTarget? TargetThatFails { get; set; }

        public ApplicationSession LoadDefault()
        {
            throw new NotSupportedException();
        }

        public string GetFilePath(
            ApplicationSession session,
            PersistenceTarget target)
        {
            return target.Kind switch
            {
                PersistenceTargetKind.AppConfig => session.AppConfigFilePath,
                PersistenceTargetKind.Repository => Path.Combine(
                    session.AppConfig.JsonFilePath,
                    session.AppConfig.RepoFileName),
                PersistenceTargetKind.FileData => session.FileDatas
                    .Single(fileData => fileData.DiskLabel == target.DiskLabel)
                    .JsonFilePath,
                _ => throw new ArgumentOutOfRangeException(nameof(target)),
            };
        }

        public void Save(
            ApplicationSession session,
            PersistenceTarget target)
        {
            SavedTargets.Add(target);
            if (target == TargetThatFails)
                throw new IOException("Simulated save failure.");
        }
    }
}
