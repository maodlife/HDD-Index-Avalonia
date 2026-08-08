using System.Collections.Generic;
using System.Linq;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;

namespace HDD_Index.Application.Declarations;

public interface IDeclarationHoldingService
{
    TreeEditResult<bool> TryDeclareHolding(
        RepoNode repoNode,
        FileNode fileNode,
        string diskLabel,
        DeclareHoldingStrategyType strategyType,
        bool saveStrategyToRepoNode);

    IReadOnlyList<DeclareHoldingValidationFailure>
        GetInvalidSaveFileNodeDatasForStrategy(
            RepoNode repoNode,
            DeclareHoldingStrategyType? strategyType);

    TreeChangeSet ApplyDeclareHoldingStrategy(
        RepoNode repoNode,
        DeclareHoldingStrategyType? strategyType,
        IEnumerable<DeclareHoldingValidationFailure> failuresToRemove);

    TreeChangeSet AbandonDeclareHoldings(
        FileNode fileNode,
        string diskLabel,
        IEnumerable<string> repoNodePaths);
}

public sealed record DeclareHoldingValidationFailure(
    string DiskLabel,
    string FileNodePath,
    string FailureReason,
    string RepoNodePath = "");

public sealed record DeclarationOperationResult(
    bool Succeeded,
    string FailureReason,
    TreeChangeSet Changes,
    IReadOnlyList<PersistenceTarget> PersistenceTargets)
{
    public static DeclarationOperationResult Success(
        TreeChangeSet changes,
        IEnumerable<PersistenceTarget> persistenceTargets)
    {
        return new DeclarationOperationResult(
            true,
            string.Empty,
            changes,
            persistenceTargets.Distinct().ToArray());
    }

    public static DeclarationOperationResult Failure(string failureReason)
    {
        return new DeclarationOperationResult(
            false,
            failureReason,
            TreeChangeSet.Empty,
            []);
    }
}

public sealed record DeclareHoldingStrategyChangePlan(
    RepoNode RepoNode,
    DeclareHoldingStrategyType? StrategyType,
    IReadOnlyList<DeclareHoldingValidationFailure> ValidationFailures);
