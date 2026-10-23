using System;
using Avalonia.Controls;

namespace HDD_Index.Views;

/// <summary>Each tree owns a distinct menu so popup ownership follows the active view.</summary>
public partial class NodeContextMenu : ContextMenu
{
    protected override Type StyleKeyOverride => typeof(ContextMenu);

    public NodeContextMenu()
    {
        InitializeComponent();
    }
}
