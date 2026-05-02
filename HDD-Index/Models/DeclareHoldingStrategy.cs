using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HDD_Index.Models;

public enum DeclareHoldingStrategyType
{
    Default,
    BDRip
}

public interface IDeclareHoldingStrategy
{
    string Name { get; }

    bool CheckDeclareHolding(
        RepoNode repoNode,
        FileNode fileNode,
        out string failureReason);
}

public static class DeclareHoldingStrategyFactory
{
    public static IDeclareHoldingStrategy Create(DeclareHoldingStrategyType strategyType)
    {
        return strategyType switch
        {
            DeclareHoldingStrategyType.Default => new DefaultDeclareHoldingStrategy(),
            DeclareHoldingStrategyType.BDRip => new BDRipDeclareHoldingStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyType), strategyType, null)
        };
    }

    public static IReadOnlyList<DeclareHoldingStrategyOption> GetAllOptions()
    {
        return Enum.GetValues<DeclareHoldingStrategyType>()
            .Select(type => new DeclareHoldingStrategyOption(type, Create(type).Name))
            .ToList();
    }
}

public sealed record DeclareHoldingStrategyOption(
    DeclareHoldingStrategyType Type,
    string Name);

public class DefaultDeclareHoldingStrategy : IDeclareHoldingStrategy
{
    public virtual string Name => "默认";

    public bool CheckDeclareHolding(
        RepoNode repoNode,
        FileNode fileNode,
        out string failureReason)
    {
        return CheckNode(repoNode, fileNode, repoNode.Name, out failureReason);
    }

    protected virtual bool CanIgnoreMissingRepoNode(RepoNode repoNode)
    {
        return false;
    }

    protected bool CheckNode(
        RepoNode repoNode,
        FileNode fileNode,
        string repoRelativePath,
        out string failureReason)
    {
        if (repoNode.Name != fileNode.Name)
        {
            failureReason =
                $"节点名称不一致：Repo 为 \"{repoNode.Name}\"，File 为 \"{fileNode.Name}\"。";
            return false;
        }

        if (repoNode.IsDirectory != fileNode.IsDirectory)
        {
            failureReason = $"节点类型不一致：\"{repoRelativePath}\"。";
            return false;
        }

        foreach (var repoChild in repoNode.Children.OfType<RepoNode>())
        {
            var matchingFileChild = fileNode.Children
                .OfType<FileNode>()
                .FirstOrDefault(f => f.Name == repoChild.Name);
            var childRelativePath = CombineRelativePath(repoRelativePath, repoChild.Name);

            if (matchingFileChild == null)
            {
                if (CanIgnoreMissingRepoNode(repoChild))
                    continue;

                failureReason = $"FileNode 缺少 RepoNode 中的节点：\"{childRelativePath}\"。";
                return false;
            }

            if (!CheckNode(repoChild, matchingFileChild, childRelativePath, out failureReason))
                return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static string CombineRelativePath(string parent, string child)
    {
        return string.IsNullOrEmpty(parent)
            ? child
            : $"{parent}/{child}";
    }
}

public sealed class BDRipDeclareHoldingStrategy : DefaultDeclareHoldingStrategy
{
    private static readonly HashSet<string> OptionalExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".ass",
        ".torrent"
    };

    public override string Name => "BDRip";

    protected override bool CanIgnoreMissingRepoNode(RepoNode repoNode)
    {
        if (!repoNode.IsDirectory)
            return OptionalExtensions.Contains(Path.GetExtension(repoNode.Name));

        return repoNode.Children.Count > 0
               && repoNode.Children
                   .OfType<RepoNode>()
                   .All(CanIgnoreMissingRepoNode);
    }
}
