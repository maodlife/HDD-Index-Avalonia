using System;
using System.Collections.Generic;
using HDD_Index.Models;

namespace HDD_Index.Application.TreeEditing;

[Flags]
public enum TreeNodePresentation
{
    None = 0,
    Name = 1,
    Relationships = 2,
    Strategy = 4,
    All = Name | Relationships | Strategy
}

public abstract record TreeChange;

public sealed record TreeNodeAdded(
    TreeNodeBase Parent,
    TreeNodeBase Node,
    int Index) : TreeChange;

public sealed record TreeNodeRemoved(
    TreeNodeBase Parent,
    TreeNodeBase Node) : TreeChange;

public sealed record TreeNodePresentationChanged(
    TreeNodeBase Node,
    TreeNodePresentation Presentation) : TreeChange;

public sealed record FileNodeSubtreeReplaced(FileNode Node) : TreeChange;

public sealed class TreeChangeSet
{
    public static TreeChangeSet Empty { get; } = new(Array.Empty<TreeChange>());

    public IReadOnlyList<TreeChange> Changes { get; }

    public bool IsEmpty => Changes.Count == 0;

    internal TreeChangeSet(IReadOnlyList<TreeChange> changes)
    {
        Changes = changes;
    }
}

public sealed class TreeChangeCollector
{
    private readonly List<TreeChange> _changes = new();

    public void AddNode(TreeNodeBase parent, TreeNodeBase node, int index)
        => _changes.Add(new TreeNodeAdded(parent, node, index));

    public void RemoveNode(TreeNodeBase parent, TreeNodeBase node)
        => _changes.Add(new TreeNodeRemoved(parent, node));

    public void Refresh(
        TreeNodeBase node,
        TreeNodePresentation presentation = TreeNodePresentation.All)
        => _changes.Add(new TreeNodePresentationChanged(node, presentation));

    public void ReplaceSubtree(FileNode node)
        => _changes.Add(new FileNodeSubtreeReplaced(node));

    public void AddRange(TreeChangeSet changeSet)
        => _changes.AddRange(changeSet.Changes);

    public TreeChangeSet Build()
        => _changes.Count == 0
            ? TreeChangeSet.Empty
            : new TreeChangeSet(_changes.ToArray());
}

public sealed record TreeEditResult<T>(
    bool Succeeded,
    T? Value,
    string FailureReason,
    TreeChangeSet Changes)
{
    public static TreeEditResult<T> Success(T value, TreeChangeSet changes)
        => new(true, value, string.Empty, changes);

    public static TreeEditResult<T> Failure(string failureReason = "")
        => new(false, default, failureReason, TreeChangeSet.Empty);
}
