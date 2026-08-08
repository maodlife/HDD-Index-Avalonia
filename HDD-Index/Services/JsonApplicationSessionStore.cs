using System;
using System.Linq;
using HDD_Index.Application.Persistence;
using HDD_Index.Models;

namespace HDD_Index.Services;

public sealed class JsonApplicationSessionStore : IApplicationSessionStore
{
    private readonly AppConfigService _appConfigService;
    private readonly TreeDataStore _treeDataStore;

    public JsonApplicationSessionStore(
        AppConfigService appConfigService,
        TreeDataStore treeDataStore)
    {
        _appConfigService = appConfigService
                            ?? throw new ArgumentNullException(nameof(appConfigService));
        _treeDataStore = treeDataStore
                         ?? throw new ArgumentNullException(nameof(treeDataStore));
    }

    public ApplicationSession LoadDefault()
    {
        return Load(_appConfigService.GetDefaultConfigPath());
    }

    public ApplicationSession Load(string appConfigFilePath)
    {
        var appConfig = _appConfigService.Load(appConfigFilePath);
        var repoNodeRoot = _treeDataStore.LoadRepoRoot(appConfig);
        var fileDatas = _treeDataStore.LoadFileDatas(appConfig);
        return new ApplicationSession(
            appConfigFilePath,
            appConfig,
            repoNodeRoot,
            fileDatas);
    }

    public string GetFilePath(
        ApplicationSession session,
        PersistenceTarget target)
    {
        return target.Kind switch
        {
            PersistenceTargetKind.AppConfig => session.AppConfigFilePath,
            PersistenceTargetKind.Repository =>
                _treeDataStore.GetRepoFilePath(session.AppConfig),
            PersistenceTargetKind.FileData => FindFileData(session, target).JsonFilePath,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    public void Save(
        ApplicationSession session,
        PersistenceTarget target)
    {
        switch (target.Kind)
        {
            case PersistenceTargetKind.AppConfig:
                _appConfigService.Save(session.AppConfigFilePath, session.AppConfig);
                break;
            case PersistenceTargetKind.Repository:
                _treeDataStore.SaveRepoRoot(
                    session.AppConfig,
                    session.RepoNodeRoot);
                break;
            case PersistenceTargetKind.FileData:
                _treeDataStore.SaveFileData(FindFileData(session, target));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }
    }

    private static FileData FindFileData(
        ApplicationSession session,
        PersistenceTarget target)
    {
        return session.FileDatas.Single(fileData =>
            string.Equals(
                fileData.DiskLabel,
                target.DiskLabel,
                StringComparison.Ordinal));
    }
}
