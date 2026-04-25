using System;
using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Services;

public static class TreeNavigationService
{
    public static RepoNodeVM? FindRepoNodeVmByPath(
        RepoNodeVM root,
        string? path,
        out IndexPath? indexPath)
    {
        var ret = TreeNodeVMBase<RepoNodeVM>.FindTreeNodeVmByPath(
            root,
            path,
            out indexPath);
        return ret as RepoNodeVM;
    }

    public static FileNodeVM? FindFileNodeVmByPath(
        FileNodeVM root,
        string? path,
        out IndexPath? indexPath)
    {
        var ret = TreeNodeVMBase<FileNodeVM>.FindTreeNodeVmByPath(
            root,
            path,
            out indexPath);
        return ret as FileNodeVM;
    }

    public static string ReplacePathPrefix(
        string path,
        string oldPrefix,
        string newPrefix)
    {
        if (string.IsNullOrWhiteSpace(oldPrefix))
            return path;

        if (string.Equals(path, oldPrefix, StringComparison.Ordinal))
            return newPrefix;

        var boundary = oldPrefix.EndsWith("/", StringComparison.Ordinal)
            ? oldPrefix
            : oldPrefix + "/";
        if (path.StartsWith(boundary, StringComparison.Ordinal))
            return newPrefix + path.Substring(oldPrefix.Length);

        return path;
    }
}
