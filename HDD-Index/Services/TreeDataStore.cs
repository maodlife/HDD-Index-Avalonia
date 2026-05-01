using System;
using System.Collections.Generic;
using System.IO;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Services;

public class TreeDataStore
{
    public RepoNode LoadRepoRoot(AppConfig appConfig)
    {
        var repoNodeFilePath = Path.Combine(
            appConfig.JsonFilePath,
            appConfig.RepoFileName);
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
        string path)
    {
        return FileDataVMBundle.CreateByPath(diskLabel, path);
    }

    private static FileDataVMBundle CreateFileDataVmBundle(
        string file,
        string localFolderPath = "")
    {
        var json = File.ReadAllText(file);
        return FileDataVMBundle.Create(
            Path.GetFileNameWithoutExtension(file),
            json,
            localFolderPath);
    }

    private static void SortByDiskLabel(List<FileDataVMBundle> bundles)
    {
        bundles.Sort((lhs, rhs) => string.Compare(
            lhs.FileData.DiskLabel,
            rhs.FileData.DiskLabel,
            StringComparison.Ordinal));
    }
}
