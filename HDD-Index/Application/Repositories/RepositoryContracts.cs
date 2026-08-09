using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Application.Repositories;

public interface IRepositoryEditingService
{
    TreeEditResult<RepoNode> CreateChildFolder(RepoNode parent);

    TreeEditResult<RepoNode> CopyFileNodeSubtreeToRepoDirectory(
        RepoNode targetParent,
        FileNode sourceFileNode);

    TreeEditResult<RepoNode> RenameRepoNode(
        RepoNode repoNode,
        string newName);

    TreeEditResult<RepoNode> DeleteRepoNode(
        RepoNode repoNode,
        RepoNode repoNodeRoot);

    IReadOnlyList<RepoNode> FindDescendantRepoNodesByName(
        RepoNode repoNode,
        string nodeName);

    TreeEditResult<int> DeleteRepoNodes(
        IEnumerable<RepoNode> repoNodes,
        RepoNode repoNodeRoot);
}

public sealed record RepositoryOperationResult(
    bool Succeeded,
    string FailureReason,
    TreeChangeSet Changes,
    IReadOnlyList<PersistenceTarget> PersistenceTargets,
    RepoNode? PreferredNode)
{
    public static RepositoryOperationResult Success(
        TreeChangeSet changes,
        IEnumerable<PersistenceTarget> persistenceTargets,
        RepoNode? preferredNode = null)
    {
        return new RepositoryOperationResult(
            true,
            string.Empty,
            changes,
            persistenceTargets.Distinct().ToArray(),
            preferredNode);
    }

    public static RepositoryOperationResult Failure(string failureReason)
    {
        return new RepositoryOperationResult(
            false,
            failureReason,
            TreeChangeSet.Empty,
            [],
            null);
    }
}

public sealed record RepositorySearchDeletePlan(
    RepoNode RepositoryRoot,
    IReadOnlyList<RepoNode> MatchedNodes,
    IReadOnlyList<string> MatchedNodePaths)
{
    public bool HasMatches => MatchedNodes.Count > 0;
}
