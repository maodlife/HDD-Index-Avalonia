using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Models;
using HDD_Index.ViewModels;
using HDD_Index.Views;

namespace HDD_Index.Adapters;

public sealed class AvaloniaRepositoryInteraction : IRepositoryInteraction
{
    private readonly Window _owner;

    public AvaloniaRepositoryInteraction(Window owner)
    {
        _owner = owner;
    }

    public async Task<DeclareHoldingStrategySelection>
        SelectInitialDeclareHoldingStrategyAsync(
            IReadOnlyList<DeclareHoldingStrategyOption> options)
    {
        var dialog = new StrategySelectionDialog(options)
        {
            Title = "选择声明持有策略",
            Width = 420,
            Height = 260,
        };
        var result = await dialog.ShowDialog<StrategySelectionDialogResult?>(_owner);
        return result == null
            ? new DeclareHoldingStrategySelection(false, null)
            : new DeclareHoldingStrategySelection(true, result.StrategyType);
    }

    public async Task<DeclareHoldingStrategySelection>
        SelectReplacementDeclareHoldingStrategyAsync(
            IReadOnlyList<DeclareHoldingStrategyOption> options,
            DeclareHoldingStrategyType? selectedStrategyType)
    {
        var dialog = new StrategySelectionDialog(
            options,
            includeClearOption: true,
            selectedStrategyType)
        {
            Title = "修改声明持有的策略",
            Width = 420,
            Height = 260,
        };
        var result = await dialog.ShowDialog<StrategySelectionDialogResult?>(_owner);
        return result == null
            ? new DeclareHoldingStrategySelection(false, null)
            : new DeclareHoldingStrategySelection(true, result.StrategyType);
    }

    public async Task<IReadOnlyList<string>?> SelectDeclareHoldingsToAbandonAsync(
        IReadOnlyList<string> repoNodePaths)
    {
        var dialog = new AbandonDeclareHoldingDialog(repoNodePaths)
        {
            Title = "放弃声明持有",
            Width = 520,
            Height = 320,
        };
        return await dialog.ShowDialog<List<string>?>(_owner);
    }

    public async Task<string?> RequestRenameAsync(string initialName)
    {
        var dialog = new RenameRepoNodeDialog(initialName)
        {
            Title = "重命名",
            Width = 420,
            Height = 160,
        };
        return await dialog.ShowDialog<string?>(_owner);
    }

    public async Task<bool> ConfirmDeleteAsync(string targetName)
    {
        var dialog = new DeleteConfirmDialog(targetName)
        {
            Title = "确认删除",
            Width = 400,
            Height = 150,
        };
        return await dialog.ShowDialog<bool>(_owner);
    }

    public async Task<string?> RequestDeleteSearchAsync()
    {
        var dialog = new SearchDeleteNodeDialog
        {
            Title = "搜索并删除节点",
            Width = 420,
            Height = 160,
        };
        return await dialog.ShowDialog<string?>(_owner);
    }

    public async Task<bool> ConfirmDeleteMatchesAsync(
        IReadOnlyList<string> matchedNodePaths)
    {
        var pathsText = string.Join(Environment.NewLine, matchedNodePaths);
        var dialog = new DeleteMatchedNodesDialog(
            $"将删除以下 {matchedNodePaths.Count} 个节点。确认删除后只会修改索引数据，不会删除真实磁盘文件。",
            pathsText)
        {
            Title = "确认删除搜索结果",
            Width = 620,
            Height = 420,
        };
        return await dialog.ShowDialog<bool>(_owner);
    }
}
