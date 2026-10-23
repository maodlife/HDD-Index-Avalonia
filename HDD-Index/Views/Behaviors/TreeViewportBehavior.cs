using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace HDD_Index.Views.Behaviors;

/// <summary>
/// Reattaching a shared source does not change the ScrollViewer's own viewport,
/// so TreeDataGrid can retain the other workspace's measured star-column widths.
/// Refresh after layout using the real content viewport, not the outer grid size.
/// </summary>
public sealed class TreeViewportBehavior : Behavior<TreeDataGrid>
{
    private object? _source;
    private Size _viewport;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } tree)
            tree.LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is { } tree)
            tree.LayoutUpdated -= OnLayoutUpdated;
        _source = null;
        base.OnDetaching();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var tree = AssociatedObject;
        if (tree?.Source is not { } source || !tree.IsEffectivelyVisible)
        {
            _source = null;
            return;
        }

        var scroller = tree.GetVisualDescendants().OfType<ScrollViewer>()
            .FirstOrDefault(x => x.Name == "PART_ScrollViewer");
        if (scroller is null || scroller.Viewport.Width <= 0)
            return;

        if (!ReferenceEquals(_source, source) || _viewport != scroller.Viewport)
        {
            _source = source;
            _viewport = scroller.Viewport;
            source.Columns.ViewportChanged(new Rect(_viewport));
        }
    }
}
