using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactions.Custom;
using Avalonia.Xaml.Interactivity;
using HDD_Index.Messages;
using ReactiveUI;

namespace HDD_Index.Views.Behaviors;

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
    private IDisposable? _subscription;
    private string _name;

    protected override void OnAttached()
    {
        base.OnAttached();
        
        _name = AssociatedObject?.Name ?? "";
        Debug.WriteLine("ScrollToSelectedItemBehavior.OnAttached: " + _name);
        
        _subscription = MessageBus.Current.Listen<TargetTreeRowMessage>()
            .Subscribe(msg =>
            {
                if (msg.TreeName == _name)
                {
                    Debug.WriteLine("TargetTreeRowMessage");
                    ScrollToSelectedItem();
                }
            });
    }

    protected override IDisposable OnAttachedToVisualTreeOverride()
    {
        var disposable = new CompositeDisposable();
        return disposable;
    }
    
    protected override void OnDetaching()
    {
        _subscription?.Dispose();
        base.OnDetaching();
    }

    private void ScrollToSelectedItem()
    {
        var index = GetCurrSelectIndex();
        ScrollToItemIndex(index);
    }

    private int GetCurrSelectIndex()
    {
        if (AssociatedObject is { RowSelection: { } rowSelection })
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
        }

        return 0;
    }

    private void ScrollToItemIndex(int index)
    {
        if (AssociatedObject is { RowsPresenter: { } rowsPresenter })
        {
            rowsPresenter.BringIntoView(index);
        }
    }
}