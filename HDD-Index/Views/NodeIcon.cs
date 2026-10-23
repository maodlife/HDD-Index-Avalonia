using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

/// <summary>
/// A small native vector icon. Width, Height, Stroke and StrokeThickness can be
/// styled like any other Path; no platform-specific symbol font is required.
/// </summary>
public class NodeIcon : Path
{
    public static readonly StyledProperty<string> KindProperty =
        AvaloniaProperty.Register<NodeIcon, string>(nameof(Kind), "Folder");

    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public NodeIcon()
    {
        Stretch = Stretch.Uniform;
        this.Bind(StrokeProperty, new DynamicResourceExtension("WorkspaceMuted"));
        StrokeThickness = 1.6;
        StrokeLineCap = PenLineCap.Round;
        StrokeJoin = PenLineJoin.Round;
        IsHitTestVisible = false;
        Data = Geometry.Parse(NodeIconGeometry.GetPath(Kind));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
            Data = Geometry.Parse(NodeIconGeometry.GetPath(Kind));
    }
}
