using System;
using System.Collections.Generic;
using System.IO;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.Startup;
using HDD_Index.Models;

namespace HDD_Index.Services;

public sealed class ApplicationStartupService : IApplicationStartupService
{
    private const string DefaultRepositoryFileName = "RepoTreeData.json";
    private readonly string _defaultConfigFilePath;
    private readonly AppConfigService _appConfigService;
    private readonly TreeDataStore _treeDataStore;
    private readonly JsonApplicationSessionStore _sessionStore;

    public ApplicationStartupService(
        string defaultConfigFilePath,
        AppConfigService appConfigService,
        TreeDataStore treeDataStore,
        JsonApplicationSessionStore sessionStore)
    {
        if (string.IsNullOrWhiteSpace(defaultConfigFilePath))
        {
            throw new ArgumentException(
                "默认配置文件路径不能为空。",
                nameof(defaultConfigFilePath));
        }

        _defaultConfigFilePath = defaultConfigFilePath;
        _appConfigService = appConfigService
                            ?? throw new ArgumentNullException(nameof(appConfigService));
        _treeDataStore = treeDataStore
                         ?? throw new ArgumentNullException(nameof(treeDataStore));
        _sessionStore = sessionStore
                        ?? throw new ArgumentNullException(nameof(sessionStore));
    }

    public ApplicationStartupResult LoadDefault()
    {
        return Load(_defaultConfigFilePath);
    }

    public ApplicationStartupResult Load(string configFilePath)
    {
        return ToStartupResult(
            configFilePath,
            _sessionStore.LoadWithDiagnostics(configFilePath));
    }

    public ApplicationStartupResult CreateDefault(string dataDirectoryPath)
    {
        if (File.Exists(_defaultConfigFilePath))
            return LoadDefault();

        if (string.IsNullOrWhiteSpace(dataDirectoryPath))
        {
            return Blocked(
                _defaultConfigFilePath,
                SessionLoadIssueKind.InitializationFailed,
                dataDirectoryPath ?? string.Empty,
                "请选择用于保存 HDD Index 数据的文件夹。");
        }

        string fullDataDirectoryPath;
        string repositoryFilePath;
        try
        {
            fullDataDirectoryPath = Path.GetFullPath(dataDirectoryPath.Trim());
            repositoryFilePath = Path.Combine(
                fullDataDirectoryPath,
                DefaultRepositoryFileName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Blocked(
                _defaultConfigFilePath,
                SessionLoadIssueKind.InitializationFailed,
                dataDirectoryPath,
                $"选择的数据目录无效：{ex.Message}");
        }

        if (File.Exists(repositoryFilePath))
        {
            return Blocked(
                _defaultConfigFilePath,
                SessionLoadIssueKind.InitializationConflict,
                repositoryFilePath,
                "所选目录已经包含 RepoTreeData.json。为避免覆盖现有数据，请选择其他目录，或退出后手工恢复对应配置。");
        }

        var appConfig = new AppConfig
        {
            JsonFilePath = fullDataDirectoryPath,
            RepoFileName = DefaultRepositoryFileName,
            FileDataFiles = [],
        };
        var repoNodeRoot = new RepoNode
        {
            Name = "Repository",
            IsDirectory = true,
        };

        try
        {
            Directory.CreateDirectory(fullDataDirectoryPath);
            _treeDataStore.SaveRepoRoot(appConfig, repoNodeRoot);
            _appConfigService.Save(_defaultConfigFilePath, appConfig);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException)
        {
            return Blocked(
                _defaultConfigFilePath,
                SessionLoadIssueKind.InitializationFailed,
                fullDataDirectoryPath,
                $"无法创建初始配置和 Repository：{ex.Message}");
        }

        return LoadDefault();
    }

    public ApplicationStartupResult RepairDataDirectory(
        string configFilePath,
        string dataDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(dataDirectoryPath))
        {
            return Blocked(
                configFilePath,
                SessionLoadIssueKind.ConfigurationInvalid,
                dataDirectoryPath ?? string.Empty,
                "请选择包含 Repository 的数据文件夹。");
        }

        AppConfig currentConfig;
        try
        {
            currentConfig = _appConfigService.Load(configFilePath);
        }
        catch
        {
            return Load(configFilePath);
        }

        string fullDataDirectoryPath;
        try
        {
            fullDataDirectoryPath = Path.GetFullPath(dataDirectoryPath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Blocked(
                configFilePath,
                SessionLoadIssueKind.ConfigurationInvalid,
                dataDirectoryPath,
                $"选择的数据目录无效：{ex.Message}");
        }

        var candidateConfig = new AppConfig
        {
            JsonFilePath = fullDataDirectoryPath,
            RepoFileName = currentConfig.RepoFileName,
            FileDataFiles = currentConfig.FileDataFiles is null
                ? []
                : new List<FileDataFileConfig>(currentConfig.FileDataFiles),
        };
        var loadResult = _sessionStore.LoadWithDiagnostics(
            configFilePath,
            candidateConfig);
        if (!loadResult.Succeeded)
            return ToStartupResult(configFilePath, loadResult);

        try
        {
            _appConfigService.Save(configFilePath, candidateConfig);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException)
        {
            return Blocked(
                configFilePath,
                SessionLoadIssueKind.ConfigurationUnreadable,
                configFilePath,
                $"新数据目录验证成功，但无法写回配置文件：{ex.Message}");
        }

        return ToStartupResult(configFilePath, loadResult);
    }

    private static ApplicationStartupResult ToStartupResult(
        string configFilePath,
        SessionLoadResult loadResult)
    {
        if (loadResult.Session != null)
        {
            return ApplicationStartupResult.Ready(
                configFilePath,
                loadResult.Session,
                loadResult.Warnings);
        }

        var issue = loadResult.BlockingIssue
                    ?? new SessionLoadIssue(
                        SessionLoadIssueKind.InitializationFailed,
                        configFilePath,
                        "启动失败，但没有可用的诊断信息。");
        return issue.Kind == SessionLoadIssueKind.ConfigurationMissing
            ? ApplicationStartupResult.FirstRun(configFilePath, issue)
            : ApplicationStartupResult.Blocked(configFilePath, issue);
    }

    private static ApplicationStartupResult Blocked(
        string configFilePath,
        SessionLoadIssueKind kind,
        string filePath,
        string message)
    {
        return ApplicationStartupResult.Blocked(
            configFilePath,
            new SessionLoadIssue(kind, filePath, message));
    }
}
