using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace HDD_Index.ViewModels;

public static class TreeDataGridSourceFactory
{
    private static readonly FuncValueConverter<bool, Geometry> DirectoryIconConverter =
        new(isDirectory => Geometry.Parse(
            isDirectory ? NodeIconGeometry.Folder : NodeIconGeometry.File));

    public static HierarchicalTreeDataGridSource<RepoNodeVM> CreateRepoSource(
        RepoNodeVM repoNodeVm)
    {
        var source = new HierarchicalTreeDataGridSource<RepoNodeVM>(repoNodeVm)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<RepoNodeVM>(
                    new TemplateColumn<RepoNodeVM>(
                        "名称",
                        new FuncDataTemplate<RepoNodeVM>((_, _) => CreateNameCell()),
                        width: new GridLength(1, GridUnitType.Star),
                        options: new TemplateColumnOptions<RepoNodeVM>
                        {
                            MinWidth = new GridLength(180),
                            CanUserResizeColumn = true,
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name,
                            CompareAscending = (x, y) => string.Compare(x?.Name, y?.Name, StringComparison.CurrentCulture),
                            CompareDescending = (x, y) => string.Compare(y?.Name, x?.Name, StringComparison.CurrentCulture),
                        }),
                    x => x.Children),
                new TemplateColumn<RepoNodeVM>(
                    "存储数",
                    new FuncDataTemplate<RepoNodeVM>((_, _) =>
                        CreateMetadataCell(nameof(RepoNodeVM.SaveFileNodeCntString), TextAlignment.Center)),
                    width: new GridLength(80),
                    options: new TemplateColumnOptions<RepoNodeVM>
                    {
                        MinWidth = new GridLength(64),
                        CanUserResizeColumn = true,
                        CompareAscending = (x, y) => string.Compare(x?.SaveFileNodeCntString, y?.SaveFileNodeCntString, StringComparison.CurrentCulture),
                        CompareDescending = (x, y) => string.Compare(y?.SaveFileNodeCntString, x?.SaveFileNodeCntString, StringComparison.CurrentCulture),
                    }),
                new TemplateColumn<RepoNodeVM>(
                    "策略",
                    new FuncDataTemplate<RepoNodeVM>((_, _) =>
                        CreateMetadataCell(nameof(RepoNodeVM.DeclareHoldingStrategyName))),
                    width: new GridLength(104),
                    options: new TemplateColumnOptions<RepoNodeVM>
                    {
                        MinWidth = new GridLength(80),
                        CanUserResizeColumn = true,
                        CompareAscending = (x, y) => string.Compare(x?.DeclareHoldingStrategyName, y?.DeclareHoldingStrategyName, StringComparison.CurrentCulture),
                        CompareDescending = (x, y) => string.Compare(y?.DeclareHoldingStrategyName, x?.DeclareHoldingStrategyName, StringComparison.CurrentCulture),
                    })
            }
        };

        if (source.RowSelection != null)
            source.RowSelection.SingleSelect = true;

        return source;
    }

    public static HierarchicalTreeDataGridSource<FileNodeVM> CreateFileSource(
        ObservableCollection<FileNodeVM> fileNodeVm)
    {
        var source = new HierarchicalTreeDataGridSource<FileNodeVM>(fileNodeVm)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileNodeVM>(
                    new TemplateColumn<FileNodeVM>(
                        "名称",
                        new FuncDataTemplate<FileNodeVM>((node, _) =>
                            CreateNameCell(showDeclarations: true, isDisk: node?.FileNode.Parent == null)),
                        width: new GridLength(1, GridUnitType.Star),
                        options: new TemplateColumnOptions<FileNodeVM>
                        {
                            MinWidth = new GridLength(180),
                            CanUserResizeColumn = true,
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name,
                        }),
                    x => x.Children)
            }
        };

        if (source.RowSelection != null)
            source.RowSelection.SingleSelect = true;

        return source;
    }

    private static TextBlock CreateMetadataCell(string propertyName, TextAlignment alignment = TextAlignment.Left)
    {
        var text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = alignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        text.Bind(TextBlock.TextProperty, new Binding(propertyName));
        return text;
    }

    private static Grid CreateNameCell(bool showDeclarations = false, bool isDisk = false)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            MinHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var icon = CreateOutlineIcon();
        icon.Margin = new Thickness(0, 0, 10, 0);
        if (isDisk)
        {
            icon.Data = Geometry.Parse(NodeIconGeometry.Disk);
        }
        else
        {
            icon.Bind(Path.DataProperty, new Binding("IsDirectory")
            {
                Converter = DirectoryIconConverter,
            });
        }

        grid.Children.Add(icon);

        var name = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        name.Bind(TextBlock.TextProperty, new Binding("Name"));
        name.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("WorkspaceText"));
        name.Bind(ToolTip.TipProperty, new Binding("Name"));
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        if (showDeclarations)
        {
            var link = CreateOutlineIcon();
            link.Width = 16;
            link.Height = 16;
            link.Margin = new Thickness(12, 0, 8, 0);
            link.Bind(Path.StrokeProperty, new DynamicResourceExtension("WorkspaceAccent"));
            link.Data = Geometry.Parse(NodeIconGeometry.Link);
            link.Bind(Control.IsVisibleProperty, new Binding("HasDeclarations"));
            ToolTip.SetTip(link, "已关联虚拟目录");
            AutomationProperties.SetName(link, "已关联虚拟目录");
            Grid.SetColumn(link, 2);
            grid.Children.Add(link);
        }

        return grid;
    }

    private static Path CreateOutlineIcon()
    {
        var icon = new Path
        {
            Width = 20,
            Height = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            IsHitTestVisible = false,
        };
        icon.Bind(Path.StrokeProperty, new DynamicResourceExtension("WorkspaceMuted"));
        return icon;
    }
}
