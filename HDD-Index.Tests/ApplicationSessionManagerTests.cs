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
                Path.Combine(session.AppConfig.JsonFilePath, "disk-b.json"),
                Path.Combine(session.AppConfig.JsonFilePath, "repo.json"),
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
        var indexPath = Path.Combine(session.AppConfig.JsonFilePath, "disk-c.json");
        session.FileDatas.Add(CreateFileData("DiskC", indexPath));

        manager.MarkDirty(PersistenceTarget.ForFileData("DiskC"));

        Assert.Equal(
            new[] { indexPath },
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
        var dataDirectory = Path.Combine(Path.GetTempPath(), "data");
        var appConfig = new AppConfig
        {
            JsonFilePath = dataDirectory,
            RepoFileName = "repo.json",
        };
        return new ApplicationSession(
            Path.Combine(dataDirectory, "config.json"),
            appConfig,
            TestTreeFactory.Repo("Repo"),
            fileDatas
            ??
            [
                CreateFileData("DiskA", Path.Combine(dataDirectory, "disk-a.json")),
                CreateFileData("DiskB", Path.Combine(dataDirectory, "disk-b.json")),
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
