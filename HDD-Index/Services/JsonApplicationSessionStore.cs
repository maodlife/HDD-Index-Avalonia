using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        var result = LoadWithDiagnostics(appConfigFilePath);
        if (result.Session != null)
            return result.Session;

        throw new InvalidOperationException(
            result.BlockingIssue?.Message ?? "无法加载应用数据。");
    }

    public SessionLoadResult LoadWithDiagnostics(string appConfigFilePath)
    {
        AppConfig appConfig;
        try
        {
            appConfig = _appConfigService.Load(appConfigFilePath);
        }
        catch (Exception ex) when (IsFileMissingException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.ConfigurationMissing,
                appConfigFilePath,
                $"找不到配置文件：{appConfigFilePath}");
        }
        catch (Exception ex) when (IsInvalidJsonException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.ConfigurationInvalid,
                appConfigFilePath,
                $"配置文件不是有效的 HDD Index 配置：{ex.Message}");
        }
        catch (Exception ex) when (IsFileAccessException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.ConfigurationUnreadable,
                appConfigFilePath,
                $"无法读取配置文件：{ex.Message}");
        }
        catch (Exception ex)
        {
            return Blocked(
                SessionLoadIssueKind.ConfigurationInvalid,
                appConfigFilePath,
                $"配置文件内容无效：{ex.Message}");
        }

        return LoadWithDiagnostics(appConfigFilePath, appConfig);
    }

    public SessionLoadResult LoadWithDiagnostics(
        string appConfigFilePath,
        AppConfig appConfig)
    {
        if (string.IsNullOrWhiteSpace(appConfig.JsonFilePath))
        {
            return Blocked(
                SessionLoadIssueKind.ConfigurationInvalid,
                appConfigFilePath,
                "配置中的 JsonFilePath 为空，无法确定数据目录。");
        }

        if (string.IsNullOrWhiteSpace(appConfig.RepoFileName))
        {
            return Blocked(
                SessionLoadIssueKind.ConfigurationInvalid,
                appConfigFilePath,
                "配置中的 RepoFileName 为空，无法确定 Repository 文件。");
        }

        string dataDirectoryPath;
        string repositoryFilePath;
        try
        {
            dataDirectoryPath = Path.GetFullPath(appConfig.JsonFilePath);
            appConfig.JsonFilePath = dataDirectoryPath;
            repositoryFilePath = _treeDataStore.GetRepoFilePath(appConfig);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Blocked(
                SessionLoadIssueKind.ConfigurationInvalid,
                appConfigFilePath,
                $"配置中的数据路径无效：{ex.Message}");
        }

        try
        {
            var attributes = File.GetAttributes(dataDirectoryPath);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                return Blocked(
                    SessionLoadIssueKind.ConfigurationInvalid,
                    appConfigFilePath,
                    $"配置中的 JsonFilePath 不是文件夹：{dataDirectoryPath}");
            }
        }
        catch (Exception ex) when (IsFileMissingException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.DataDirectoryMissing,
                dataDirectoryPath,
                $"找不到数据目录：{dataDirectoryPath}");
        }
        catch (Exception ex) when (IsFileAccessException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.DataDirectoryUnreadable,
                dataDirectoryPath,
                $"无法访问数据目录：{ex.Message}");
        }

        RepoNode repoNodeRoot;
        try
        {
            repoNodeRoot = _treeDataStore.LoadRepoRoot(appConfig);
        }
        catch (Exception ex) when (IsFileMissingException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.RepositoryMissing,
                repositoryFilePath,
                $"找不到 Repository 文件：{repositoryFilePath}");
        }
        catch (Exception ex) when (IsInvalidJsonException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.RepositoryInvalid,
                repositoryFilePath,
                $"Repository 文件内容无效：{ex.Message}");
        }
        catch (Exception ex) when (IsFileAccessException(ex))
        {
            return Blocked(
                SessionLoadIssueKind.RepositoryUnreadable,
                repositoryFilePath,
                $"无法读取 Repository 文件：{ex.Message}");
        }
        catch (Exception ex)
        {
            return Blocked(
                SessionLoadIssueKind.RepositoryInvalid,
                repositoryFilePath,
                $"Repository 文件内容无效：{ex.Message}");
        }

        var warnings = new List<SessionLoadIssue>();
        var fileDatas = new List<FileData>();
        appConfig.FileDataFiles ??= [];
        if (appConfig.FileDataFiles.Count > 0)
        {
            foreach (var fileDataConfig in appConfig.FileDataFiles)
                TryLoadConfiguredFileData(appConfig, fileDataConfig, fileDatas, warnings);
        }
        else
        {
            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(dataDirectoryPath);
            }
            catch (Exception ex) when (IsFileAccessException(ex))
            {
                return Blocked(
                    SessionLoadIssueKind.DataDirectoryUnreadable,
                    dataDirectoryPath,
                    $"无法列出数据目录中的索引文件：{ex.Message}");
            }

            foreach (var filePath in filePaths)
            {
                if (string.Equals(
                        Path.GetFullPath(filePath),
                        Path.GetFullPath(repositoryFilePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryLoadFileData(filePath, string.Empty, fileDatas, warnings);
            }
        }

        fileDatas.Sort((lhs, rhs) => string.Compare(
            lhs.DiskLabel,
            rhs.DiskLabel,
            StringComparison.Ordinal));
        return SessionLoadResult.Success(
            new ApplicationSession(
                appConfigFilePath,
                appConfig,
                repoNodeRoot,
                fileDatas),
            warnings);
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

    private static SessionLoadResult Blocked(
        SessionLoadIssueKind kind,
        string filePath,
        string message)
    {
        return SessionLoadResult.Blocked(
            new SessionLoadIssue(kind, filePath, message));
    }

    private void TryLoadConfiguredFileData(
        AppConfig appConfig,
        FileDataFileConfig? fileDataConfig,
        ICollection<FileData> fileDatas,
        ICollection<SessionLoadIssue> warnings)
    {
        if (fileDataConfig == null)
        {
            warnings.Add(new SessionLoadIssue(
                SessionLoadIssueKind.FileIndexConfigurationInvalid,
                appConfig.JsonFilePath,
                "配置中有一个空的磁盘索引条目，已跳过。"));
            return;
        }

        if (string.IsNullOrWhiteSpace(fileDataConfig.JsonFilePath))
        {
            warnings.Add(new SessionLoadIssue(
                SessionLoadIssueKind.FileIndexConfigurationInvalid,
                appConfig.JsonFilePath,
                "配置中有一个磁盘索引缺少 JsonFilePath，已跳过。"));
            return;
        }

        string filePath;
        try
        {
            filePath = Path.GetFullPath(Path.Combine(
                appConfig.JsonFilePath,
                fileDataConfig.JsonFilePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            warnings.Add(new SessionLoadIssue(
                SessionLoadIssueKind.FileIndexConfigurationInvalid,
                fileDataConfig.JsonFilePath,
                $"磁盘索引路径无效，已跳过：{ex.Message}",
                Path.GetFileNameWithoutExtension(fileDataConfig.JsonFilePath)));
            return;
        }

        TryLoadFileData(
            filePath,
            fileDataConfig.LocalFolderPath,
            fileDatas,
            warnings);
    }

    private void TryLoadFileData(
        string filePath,
        string localFolderPath,
        ICollection<FileData> fileDatas,
        ICollection<SessionLoadIssue> warnings)
    {
        var diskLabel = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            fileDatas.Add(_treeDataStore.LoadFileData(filePath, localFolderPath));
        }
        catch (Exception ex) when (IsFileMissingException(ex))
        {
            warnings.Add(new SessionLoadIssue(
                SessionLoadIssueKind.FileIndexMissing,
                filePath,
                $"找不到磁盘索引 {diskLabel}，本次启动已跳过。",
                diskLabel));
        }
        catch (Exception ex) when (IsInvalidJsonException(ex))
        {
            warnings.Add(new SessionLoadIssue(
                SessionLoadIssueKind.FileIndexInvalid,
                filePath,
                $"磁盘索引 {diskLabel} 内容无效，本次启动已跳过：{ex.Message}",
                diskLabel));
        }
        catch (Exception ex) when (IsFileAccessException(ex))
        {
            warnings.Add(new SessionLoadIssue(
                SessionLoadIssueKind.FileIndexUnreadable,
                filePath,
                $"无法读取磁盘索引 {diskLabel}，本次启动已跳过：{ex.Message}",
                diskLabel));
        }
        catch (Exception ex)
        {
            warnings.Add(new SessionLoadIssue(
                SessionLoadIssueKind.FileIndexInvalid,
                filePath,
                $"磁盘索引 {diskLabel} 内容无效，本次启动已跳过：{ex.Message}",
                diskLabel));
        }
    }

    private static bool IsInvalidJsonException(Exception exception)
    {
        return exception is JsonException
            or NotSupportedException
            or InvalidOperationException;
    }

    private static bool IsFileAccessException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static bool IsFileMissingException(Exception exception)
    {
        return exception is FileNotFoundException or DirectoryNotFoundException;
    }
}
