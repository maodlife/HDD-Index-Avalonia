using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HDD_Index.Application.FileScanning;
using HDD_Index.Models;

namespace HDD_Index.Application.ExternalInteractions;

public enum MessageDisplayKind
{
    Standard,
    Detailed
}

public sealed record MessageRequest(
    string Message,
    MessageDisplayKind DisplayKind = MessageDisplayKind.Standard);

public sealed record ConfirmationRequest(
    string Message,
    string Title = "确认");

public interface IUserInteraction
{
    Task ShowMessageAsync(MessageRequest request);

    Task<bool> ConfirmAsync(ConfirmationRequest request);
}

public sealed record DeclareHoldingStrategySelection(
    bool IsAccepted,
    DeclareHoldingStrategyType? StrategyType);

public interface IRepositoryInteraction
{
    Task<DeclareHoldingStrategySelection> SelectInitialDeclareHoldingStrategyAsync(
        IReadOnlyList<DeclareHoldingStrategyOption> options);

    Task<DeclareHoldingStrategySelection> SelectReplacementDeclareHoldingStrategyAsync(
        IReadOnlyList<DeclareHoldingStrategyOption> options,
        DeclareHoldingStrategyType? selectedStrategyType);

    Task<IReadOnlyList<string>?> SelectDeclareHoldingsToAbandonAsync(
        IReadOnlyList<string> repoNodePaths);

    Task<string?> RequestRenameAsync(string initialName);

    Task<bool> ConfirmDeleteAsync(string targetName);

    Task<string?> RequestDeleteSearchAsync();

    Task<bool> ConfirmDeleteMatchesAsync(IReadOnlyList<string> matchedNodePaths);
}

public sealed record NewFileTreeSelection(
    string Path,
    string DiskLabel);

public interface IFileTreeInteraction
{
    Task<bool> ConfirmDeleteAsync(string targetName);

    Task<NewFileTreeSelection?> RequestNewFileTreeAsync();
}

public interface IFileTreeScanProgressRunner
{
    Task<FileTreeScanExecutionResult<T>> RunAsync<T>(
        Func<IProgress<FileTreeScanProgress>, CancellationToken, T> scan);
}

public sealed record FileTreeScanExecutionResult<T>(
    T? Value,
    bool IsCancelled,
    Exception? Error);

public interface IPathOpener
{
    void OpenFolder(string folderPath);

    void ShowPathInFolder(string path);
}
