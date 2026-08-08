using System;
using System.Collections.Generic;
using HDD_Index.Models;

namespace HDD_Index.Application.Persistence;

public sealed class ApplicationSession
{
    public ApplicationSession(
        string appConfigFilePath,
        AppConfig appConfig,
        RepoNode repoNodeRoot,
        List<FileData> fileDatas)
    {
        if (string.IsNullOrWhiteSpace(appConfigFilePath))
            throw new ArgumentException(
                "配置文件路径不能为空。",
                nameof(appConfigFilePath));

        AppConfigFilePath = appConfigFilePath;
        AppConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
        RepoNodeRoot = repoNodeRoot ?? throw new ArgumentNullException(nameof(repoNodeRoot));
        FileDatas = fileDatas ?? throw new ArgumentNullException(nameof(fileDatas));
    }

    public string AppConfigFilePath { get; }

    public AppConfig AppConfig { get; }

    public RepoNode RepoNodeRoot { get; }

    public List<FileData> FileDatas { get; }
}
