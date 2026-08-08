using System;
using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Persistence;
using HDD_Index.Models;

namespace HDD_Index.Application.Declarations;

public sealed class DeclarationUseCases
{
    private readonly IDeclarationHoldingService _declarationService;

    public DeclarationUseCases(IDeclarationHoldingService declarationService)
    {
        _declarationService = declarationService
                              ?? throw new ArgumentNullException(nameof(declarationService));
    }

    public DeclarationOperationResult DeclareHolding(
        RepoNode repoNode,
        FileNode fileNode,
        string diskLabel,
        DeclareHoldingStrategyType? selectedInitialStrategyType)
    {
        if (string.IsNullOrWhiteSpace(diskLabel))
            return DeclarationOperationResult.Failure("磁盘标签不能为空。");

        var saveStrategyToRepoNode = repoNode.DeclareHoldingStrategyType == null;
        var strategyType = repoNode.DeclareHoldingStrategyType
                           ?? selectedInitialStrategyType;
        if (strategyType == null)
            return DeclarationOperationResult.Failure("未选择声明持有策略。");

        var result = _declarationService.TryDeclareHolding(
            repoNode,
            fileNode,
            diskLabel,
            strategyType.Value,
            saveStrategyToRepoNode);
        if (!result.Succeeded)
            return DeclarationOperationResult.Failure(result.FailureReason);

        return DeclarationOperationResult.Success(
            result.Changes,
            [
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData(diskLabel),
            ]);
    }

    public IReadOnlyList<string> GetDeclaredRepoNodePaths(FileNode fileNode)
    {
        return fileNode.DeclareRepoNodeDatas
            .Select(data => data.RepoNodePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .ToList();
    }

    public DeclarationOperationResult AbandonDeclareHoldings(
        FileNode fileNode,
        string diskLabel,
        IReadOnlyList<string> selectedRepoNodePaths)
    {
        if (string.IsNullOrWhiteSpace(diskLabel))
            return DeclarationOperationResult.Failure("磁盘标签不能为空。");

        if (selectedRepoNodePaths.Count == 0)
            return DeclarationOperationResult.Failure("未选择要放弃的声明持有关系。");

        var changes = _declarationService.AbandonDeclareHoldings(
            fileNode,
            diskLabel,
            selectedRepoNodePaths);
        return DeclarationOperationResult.Success(
            changes,
            [
                PersistenceTarget.Repository,
                PersistenceTarget.ForFileData(diskLabel),
            ]);
    }

    public DeclareHoldingStrategyChangePlan PlanStrategyChange(
        RepoNode repoNode,
        DeclareHoldingStrategyType? strategyType)
    {
        var failures = _declarationService
            .GetInvalidSaveFileNodeDatasForStrategy(repoNode, strategyType);
        return new DeclareHoldingStrategyChangePlan(
            repoNode,
            strategyType,
            failures.ToArray());
    }

    public DeclarationOperationResult ApplyStrategyChange(
        DeclareHoldingStrategyChangePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var changes = _declarationService.ApplyDeclareHoldingStrategy(
            plan.RepoNode,
            plan.StrategyType,
            plan.ValidationFailures);
        var persistenceTargets = new List<PersistenceTarget>
        {
            PersistenceTarget.Repository,
        };
        persistenceTargets.AddRange(
            plan.ValidationFailures
                .Select(failure => failure.DiskLabel)
                .Where(diskLabel => !string.IsNullOrWhiteSpace(diskLabel))
                .Distinct()
                .Select(PersistenceTarget.ForFileData));

        return DeclarationOperationResult.Success(
            changes,
            persistenceTargets);
    }
}
