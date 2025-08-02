using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactions.Custom;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace HDD_Index.Views.Behaviors;

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<
    Avalonia.Controls.TreeDataGrid>
{
    private readonly CompositeDisposable _disposable = new();
    private IDisposable? _selectionChangedSubscription;

    protected override IDisposable OnAttachedToVisualTreeOverride()
    {
        RegisterSelectionHandler();

        // 监听 Source 的变化
        AssociatedObject?.GetObservable(TreeDataGrid.SourceProperty)
            .Subscribe(_ =>
            {
                Console.WriteLine(
                    "TreeDataGrid.Source changed, re-registering selection handler.");
                _selectionChangedSubscription?.Dispose(); // 清理旧的订阅
                RegisterSelectionHandler();
            })
            .DisposeWith(_disposable);

        return _disposable;
    }

    private void RegisterSelectionHandler()
    {
        if (AssociatedObject?.RowSelection is { } rowSelection)
        {
            _selectionChangedSubscription = Observable
                .FromEventPattern(
                    rowSelection,
                    nameof(rowSelection.SelectionChanged))
                .Select(_ =>
                {
                    var selectedIndexPath =
                        rowSelection.SelectedIndex.FirstOrDefault();
                    if (AssociatedObject.Rows is null)
                        return selectedIndexPath;

                    var rowIndex =
                        AssociatedObject.Rows.ModelIndexToRowIndex(
                            selectedIndexPath);

                    if (rowSelection.SelectedIndex.Count > 1)
                    {
                        rowIndex += rowSelection.SelectedIndex.Skip(1).Sum();
                        rowIndex += 1;
                    }

                    return rowIndex;
                })
                .WhereNotNull()
                .Do(ScrollToItemIndex)
                .Subscribe();
        }
    }

    private void ScrollToItemIndex(int index)
    {
        Console.WriteLine("ScrollToItemIndex: " + index);
        if (AssociatedObject?.RowsPresenter is { } rowsPresenter)
        {
            rowsPresenter.BringIntoView(index);
        }
    }
}