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
        var files = Directory.GetFiles(appConfig.JsonFilePath);
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == appConfig.RepoFileName)
                continue;

            var json = File.ReadAllText(file);
            var bundle = FileDataVMBundle.Create(
                Path.GetFileNameWithoutExtension(file),
                json);
            bundles.Add(bundle);
        }

        bundles.Sort((lhs, rhs) => string.Compare(
            lhs.FileData.DiskLabel,
            rhs.FileData.DiskLabel,
            StringComparison.Ordinal));

        return bundles;
    }

    public FileDataVMBundle CreateFileDataVmBundleFromPath(
        string diskLabel,
        string path)
    {
        return FileDataVMBundle.CreateByPath(diskLabel, path);
    }
}
