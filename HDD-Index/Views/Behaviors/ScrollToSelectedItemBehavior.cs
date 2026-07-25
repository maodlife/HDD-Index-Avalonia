using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using Avalonia.Xaml.Interactions.Custom;
using HDD_Index.Messages;
using ReactiveUI;

namespace HDD_Index.Views.Behaviors;

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
    private IDisposable? _subscription;
    private string _name = string.Empty;

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
            if (AssociatedObject.Rows is null)
            {
                return 0;
            }

            var rowIndex = AssociatedObject.Rows.ModelIndexToRowIndex(rowSelection.SelectedIndex);
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
