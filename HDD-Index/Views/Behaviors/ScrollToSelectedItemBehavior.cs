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

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
    protected override IDisposable OnAttachedToVisualTreeOverride()
    {
        var disposable = new CompositeDisposable();
        
        if (AssociatedObject is { RowSelection: { } rowSelection })
        {
            Observable.FromEventPattern(rowSelection, nameof(rowSelection.SelectionChanged))
                .Select(x =>
                {
                    var selectedIndexPath = rowSelection.SelectedIndex.FirstOrDefault();
                    if (AssociatedObject.Rows is null)
                    {
                        return selectedIndexPath;
                    }

                    // Get the actual index in the list of items.
                    var rowIndex = AssociatedObject.Rows.ModelIndexToRowIndex(selectedIndexPath);

                    // Correct the index wih the index of child item, in the case when the selected item is a child.
                    if (rowSelection.SelectedIndex.Count > 1)
                    {
                        // Skip 1 because the first index is the parent.
                        // Every other index is the child index.
                        rowIndex += rowSelection.SelectedIndex.Skip(1).Sum();

                        // Need to add 1 to get the correct index.
                        rowIndex += 1;
                    }

                    return rowIndex;
                })
                .WhereNotNull()
                .Do(ScrollToItemIndex)
                .Subscribe()
                .DisposeWith(disposable);
        }
        
        return disposable;
    }

    private void ScrollToItemIndex(int index)
    {
        Console.WriteLine("ScrollToItemIndex" + index);
        if (AssociatedObject is { RowsPresenter: { } rowsPresenter })
        {
            rowsPresenter.BringIntoView(index);
        }
    }
}