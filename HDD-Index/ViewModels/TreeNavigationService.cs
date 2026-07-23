using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace HDD_Index.ViewModels;

public static class TreeNavigationService
{
    public static IReadOnlyList<IndexPath> FindRepoExpandPathsToSavedNodes(
        RepoNodeVM root)
    {
        return FindExpandPathsToMatchingNodes(
            root,
            x => x.RepoNode.SaveFileNodeDatas.Count > 0);
    }

    public static IReadOnlyList<IndexPath> FindFileExpandPathsToDeclaredNodes(
        FileNodeVM root)
    {
        return FindExpandPathsToMatchingNodes(
            root,
            x => x.FileNode.DeclareRepoNodeDatas.Count > 0);
    }

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
        return Models.TreeNodeUtils.ReplacePathPrefix(path, oldPrefix, newPrefix);
    }

    private static IReadOnlyList<IndexPath> FindExpandPathsToMatchingNodes<T>(
        T root,
        Func<T, bool> isTarget)
        where T : TreeNodeVMBase<T>
    {
        var expandPaths = new List<IndexPath>();
        CollectExpandPathsToMatchingNodes(
            root,
            isTarget,
            new List<int> { 0 },
            expandPaths);
        expandPaths.Sort((left, right) => left.Count.CompareTo(right.Count));
        return expandPaths;
    }

    private static bool CollectExpandPathsToMatchingNodes<T>(
        T node,
        Func<T, bool> isTarget,
        List<int> indexSegments,
        ICollection<IndexPath> expandPaths)
        where T : TreeNodeVMBase<T>
    {
        if (isTarget(node))
            return true;

        var hasTargetDescendant = false;
        var shouldAddCurrentPath = true;
        for (var i = 0; i < node.Children.Count; i++)
        {
            indexSegments.Add(i);
            var childHasTarget = CollectExpandPathsToMatchingNodes(
                node.Children[i],
                isTarget,
                indexSegments,
                expandPaths);
            indexSegments.RemoveAt(indexSegments.Count - 1);

            if (!childHasTarget)
                continue;

            hasTargetDescendant = true;
            if (shouldAddCurrentPath)
            {
                expandPaths.Add(new IndexPath(indexSegments));
                shouldAddCurrentPath = false;
            }
        }

        return hasTargetDescendant;
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
