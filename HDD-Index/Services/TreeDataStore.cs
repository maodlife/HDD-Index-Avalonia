using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using HDD_Index.Models;
using HDD_Index.ViewModels;

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

    public List<FileDataVMBundle> LoadFileDataVmBundles(AppConfig appConfig)
    {
        var bundles = new List<FileDataVMBundle>();
        if (appConfig.FileDataFiles.Count > 0)
        {
            foreach (var fileDataConfig in appConfig.FileDataFiles)
            {
                if (string.IsNullOrWhiteSpace(fileDataConfig.JsonFilePath))
                    continue;

                var file = Path.Combine(
                    appConfig.JsonFilePath,
                    fileDataConfig.JsonFilePath);
                bundles.Add(CreateFileDataVmBundle(
                    file,
                    fileDataConfig.LocalFolderPath));
            }

            SortByDiskLabel(bundles);
            return bundles;
        }

        var files = Directory.GetFiles(appConfig.JsonFilePath);
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == appConfig.RepoFileName)
                continue;

            bundles.Add(CreateFileDataVmBundle(file));
        }

        SortByDiskLabel(bundles);
        return bundles;
    }

    public FileDataVMBundle CreateFileDataVmBundleFromPath(
        string diskLabel,
        string path,
        string jsonFilePath)
    {
        return FileDataVMBundle.CreateByPath(diskLabel, path, jsonFilePath);
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

    public void SaveFileDataBundle(FileDataVMBundle bundle)
    {
        var jsonFilePath = bundle.FileData.JsonFilePath;
        if (string.IsNullOrWhiteSpace(jsonFilePath))
            throw new InvalidOperationException(
                $"无法保存磁盘 {bundle.FileData.DiskLabel}: 缺少 JSON 文件路径。");

        var json = JsonSerializer.Serialize(
            bundle.FileData.FileNodeRoot,
            JsonOptions);
        File.WriteAllText(jsonFilePath, json);
    }

    private static FileDataVMBundle CreateFileDataVmBundle(
        string file,
        string localFolderPath = "")
    {
        var json = File.ReadAllText(file);
        var bundle = FileDataVMBundle.Create(
            Path.GetFileNameWithoutExtension(file),
            json,
            localFolderPath);
        bundle.FileData.JsonFilePath = file;
        return bundle;
    }

    private static void SortByDiskLabel(List<FileDataVMBundle> bundles)
    {
        bundles.Sort((lhs, rhs) => string.Compare(
            lhs.FileData.DiskLabel,
            rhs.FileData.DiskLabel,
            StringComparison.Ordinal));
    }
}
