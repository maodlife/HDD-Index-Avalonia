using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Application.Repositories;

public sealed class RepositoryUseCases
{
    private readonly IRepositoryEditingService _repositoryEditingService;
    private readonly IReadOnlyCollection<FileData> _fileDatas;

    public RepositoryUseCases(
        IRepositoryEditingService repositoryEditingService,
        IReadOnlyCollection<FileData> fileDatas)
    {
        _repositoryEditingService = repositoryEditingService
                                    ?? throw new ArgumentNullException(
                                        nameof(repositoryEditingService));
        _fileDatas = fileDatas
                     ?? throw new ArgumentNullException(nameof(fileDatas));
    }

    public RepositoryOperationResult CreateChildFolder(RepoNode parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var result = _repositoryEditingService.CreateChildFolder(parent);
        return FromNodeResult(result, GetRepositoryAndAllFileDataTargets());
    }

    public RepositoryOperationResult CopyFileNodeSubtreeToRepoDirectory(
        RepoNode targetParent,
        FileNode sourceFileNode)
    {
        ArgumentNullException.ThrowIfNull(targetParent);
        ArgumentNullException.ThrowIfNull(sourceFileNode);

        var result = _repositoryEditingService.CopyFileNodeSubtreeToRepoDirectory(
            targetParent,
            sourceFileNode);
        return FromNodeResult(
            result,
            [PersistenceTarget.Repository]);
    }

    public RepositoryOperationResult RenameRepoNode(
        RepoNode repoNode,
        string newName)
    {
        ArgumentNullException.ThrowIfNull(repoNode);

        var result = _repositoryEditingService.RenameRepoNode(repoNode, newName);
        return FromNodeResult(result, GetRepositoryAndAllFileDataTargets());
    }

    public RepositoryOperationResult DeleteRepoNode(
        RepoNode repoNode,
        RepoNode repoNodeRoot)
    {
        ArgumentNullException.ThrowIfNull(repoNode);
        ArgumentNullException.ThrowIfNull(repoNodeRoot);

        var result = _repositoryEditingService.DeleteRepoNode(
            repoNode,
            repoNodeRoot);
        if (!result.Succeeded)
            return RepositoryOperationResult.Failure(result.FailureReason);

        return RepositoryOperationResult.Success(
            result.Changes,
            GetRepositoryAndAllFileDataTargets());
    }

    public RepositorySearchDeletePlan PlanSearchDelete(
        RepoNode selectedNode,
        RepoNode repositoryRoot,
        string searchText)
    {
        ArgumentNullException.ThrowIfNull(selectedNode);
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        var matchedNodes = _repositoryEditingService
            .FindDescendantRepoNodesByName(selectedNode, searchText)
            .ToArray();
        return new RepositorySearchDeletePlan(
            repositoryRoot,
            matchedNodes,
            matchedNodes.Select(node => node.GetPath()).ToArray());
    }

    public RepositoryOperationResult ApplySearchDelete(
        RepositorySearchDeletePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.HasMatches)
            return RepositoryOperationResult.Failure("没有找到要删除的节点。");

        var result = _repositoryEditingService.DeleteRepoNodes(
            plan.MatchedNodes,
            plan.RepositoryRoot);
        if (!result.Succeeded)
            return RepositoryOperationResult.Failure(result.FailureReason);

        return RepositoryOperationResult.Success(
            result.Changes,
            GetRepositoryAndAllFileDataTargets());
    }

    private static RepositoryOperationResult FromNodeResult(
        TreeEditResult<RepoNode> result,
        IEnumerable<PersistenceTarget> persistenceTargets)
    {
        if (!result.Succeeded || result.Value == null)
            return RepositoryOperationResult.Failure(result.FailureReason);

        return RepositoryOperationResult.Success(
            result.Changes,
            persistenceTargets,
            result.Value);
    }

    private IEnumerable<PersistenceTarget> GetRepositoryAndAllFileDataTargets()
    {
        yield return PersistenceTarget.Repository;

        foreach (var diskLabel in _fileDatas
                     .Select(fileData => fileData.DiskLabel)
                     .Where(diskLabel => !string.IsNullOrWhiteSpace(diskLabel))
                     .Distinct())
        {
            yield return PersistenceTarget.ForFileData(diskLabel);
        }
    }
}
