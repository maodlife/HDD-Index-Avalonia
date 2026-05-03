using System;
using System.Collections.Generic;
using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Services;

public static class TreeNavigationService
{
    public static IReadOnlyList<RepoNodeSearchMatch> FindRepoNodeVmsByNameContains(
        RepoNodeVM root,
        string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Array.Empty<RepoNodeSearchMatch>();

        var matches = new List<RepoNodeSearchMatch>();
        CollectRepoNodeNameMatches(
            root,
            searchText.Trim(),
            new List<int> { 0 },
            matches);
        return matches;
    }

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

    private static void CollectRepoNodeNameMatches(
        RepoNodeVM repoNodeVm,
        string searchText,
        List<int> indexSegments,
        ICollection<RepoNodeSearchMatch> matches)
    {
        if (repoNodeVm.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            matches.Add(new RepoNodeSearchMatch(
                repoNodeVm,
                new IndexPath(indexSegments)));

        for (var i = 0; i < repoNodeVm.Children.Count; i++)
        {
            indexSegments.Add(i);
            CollectRepoNodeNameMatches(
                repoNodeVm.Children[i],
                searchText,
                indexSegments,
                matches);
            indexSegments.RemoveAt(indexSegments.Count - 1);
        }
    }
}
