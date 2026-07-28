using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using HDD_Index.Models;

namespace HDD_Index.Services;

public class TreeDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public RepoNode LoadRepoRoot(AppConfig appConfig)
    {
        var repoNodeFilePath = GetRepoFilePath(appConfig);
        var json = File.ReadAllText(repoNodeFilePath);
        var root = RepoNode.CreateByJson(json);
        if (root == null)
            throw new InvalidOperationException($"无法读取仓库树: {repoNodeFilePath}");

        return root;
    }

    public List<FileData> LoadFileDatas(AppConfig appConfig)
    {
        var fileDatas = new List<FileData>();
        if (appConfig.FileDataFiles.Count > 0)
        {
            foreach (var fileDataConfig in appConfig.FileDataFiles)
            {
                if (string.IsNullOrWhiteSpace(fileDataConfig.JsonFilePath))
                    continue;

                var file = Path.Combine(
                    appConfig.JsonFilePath,
                    fileDataConfig.JsonFilePath);
                fileDatas.Add(CreateFileData(
                    file,
                    fileDataConfig.LocalFolderPath));
            }

            SortByDiskLabel(fileDatas);
            return fileDatas;
        }

        var files = Directory.GetFiles(appConfig.JsonFilePath);
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == appConfig.RepoFileName)
                continue;

            fileDatas.Add(CreateFileData(file));
        }

        SortByDiskLabel(fileDatas);
        return fileDatas;
    }

    public string GetRepoFilePath(AppConfig appConfig)
    {
        return Path.Combine(
            appConfig.JsonFilePath,
            appConfig.RepoFileName);
    }

    public void SaveRepoRoot(AppConfig appConfig, RepoNode repoNodeRoot)
    {
        var json = JsonSerializer.Serialize(repoNodeRoot, JsonOptions);
        File.WriteAllText(GetRepoFilePath(appConfig), json);
    }

    public void SaveFileData(FileData fileData)
    {
        var jsonFilePath = fileData.JsonFilePath;
        if (string.IsNullOrWhiteSpace(jsonFilePath))
            throw new InvalidOperationException(
                $"无法保存磁盘 {fileData.DiskLabel}: 缺少 JSON 文件路径。");

        var json = JsonSerializer.Serialize(
            fileData.FileNodeRoot,
            JsonOptions);
        File.WriteAllText(jsonFilePath, json);
    }

    private static FileData CreateFileData(
        string file,
        string localFolderPath = "")
    {
        var json = File.ReadAllText(file);
        var root = FileNode.CreateByJson(json);
        if (root == null)
            throw new InvalidOperationException($"无法读取文件树: {file}");

        return new FileData
        {
            DiskLabel = Path.GetFileNameWithoutExtension(file),
            LocalFolderPath = localFolderPath,
            JsonFilePath = file,
            FileNodeRoot = root
        };
    }

    private static void SortByDiskLabel(List<FileData> fileDatas)
    {
        fileDatas.Sort((lhs, rhs) => string.Compare(
            lhs.DiskLabel,
            rhs.DiskLabel,
            StringComparison.Ordinal));
    }
}
