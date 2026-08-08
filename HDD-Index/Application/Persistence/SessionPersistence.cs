using System;
using System.Collections.Generic;
using System.Linq;

namespace HDD_Index.Application.Persistence;

public enum PersistenceTargetKind
{
    Unknown,
    AppConfig,
    Repository,
    FileData
}

public readonly record struct PersistenceTarget
{
    private PersistenceTarget(
        PersistenceTargetKind kind,
        string diskLabel)
    {
        Kind = kind;
        DiskLabel = diskLabel;
    }

    public PersistenceTargetKind Kind { get; }

    public string DiskLabel { get; }

    public static PersistenceTarget AppConfig { get; } =
        new(PersistenceTargetKind.AppConfig, string.Empty);

    public static PersistenceTarget Repository { get; } =
        new(PersistenceTargetKind.Repository, string.Empty);

    public static PersistenceTarget ForFileData(string diskLabel)
    {
        if (string.IsNullOrWhiteSpace(diskLabel))
            throw new ArgumentException("磁盘标签不能为空。", nameof(diskLabel));

        return new PersistenceTarget(PersistenceTargetKind.FileData, diskLabel);
    }
}

public interface IApplicationSessionStore
{
    ApplicationSession LoadDefault();

    string GetFilePath(
        ApplicationSession session,
        PersistenceTarget target);

    void Save(
        ApplicationSession session,
        PersistenceTarget target);
}

public sealed class ApplicationSessionManager
{
    private readonly IApplicationSessionStore _store;
    private readonly HashSet<PersistenceTarget> _dirtyTargets = [];

    public ApplicationSessionManager(
        ApplicationSession session,
        IApplicationSessionStore store)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ApplicationSession Session { get; }

    public bool HasDirtyFiles => _dirtyTargets.Count > 0;

    public void MarkDirty(params PersistenceTarget[] targets)
    {
        MarkDirty((IEnumerable<PersistenceTarget>)targets);
    }

    public void MarkDirty(IEnumerable<PersistenceTarget> targets)
    {
        foreach (var target in targets)
        {
            if (CanResolve(target))
                _dirtyTargets.Add(target);
        }
    }

    public void MarkAllFileDataDirty()
    {
        foreach (var fileData in Session.FileDatas)
            _dirtyTargets.Add(PersistenceTarget.ForFileData(fileData.DiskLabel));
    }

    public IReadOnlyList<string> GetDirtyFilePaths()
    {
        return EnumerateDirtyTargetsInSaveOrder()
            .Select(target => _store.GetFilePath(Session, target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SaveDirtyFiles()
    {
        var targets = EnumerateDirtyTargetsInSaveOrder().ToList();
        foreach (var target in targets)
            _store.Save(Session, target);

        _dirtyTargets.ExceptWith(targets);
    }

    private bool CanResolve(PersistenceTarget target)
    {
        return target.Kind switch
        {
            PersistenceTargetKind.AppConfig => true,
            PersistenceTargetKind.Repository => true,
            PersistenceTargetKind.FileData => Session.FileDatas.Any(fileData =>
                string.Equals(
                    fileData.DiskLabel,
                    target.DiskLabel,
                    StringComparison.Ordinal)),
            _ => false,
        };
    }

    private IEnumerable<PersistenceTarget> EnumerateDirtyTargetsInSaveOrder()
    {
        if (_dirtyTargets.Contains(PersistenceTarget.AppConfig))
            yield return PersistenceTarget.AppConfig;

        if (_dirtyTargets.Contains(PersistenceTarget.Repository))
            yield return PersistenceTarget.Repository;

        foreach (var fileData in Session.FileDatas)
        {
            var target = PersistenceTarget.ForFileData(fileData.DiskLabel);
            if (_dirtyTargets.Contains(target))
                yield return target;
        }
    }
}
