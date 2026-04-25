using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;

namespace HDD_Index.ViewModels;

public static class TreeDataGridSourceFactory
{
    public static HierarchicalTreeDataGridSource<RepoNodeVM> CreateRepoSource(
        RepoNodeVM repoNodeVm)
    {
        return new HierarchicalTreeDataGridSource<RepoNodeVM>(repoNodeVm)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<RepoNodeVM>(
                    new TextColumn<RepoNodeVM, string>(
                        "Name",
                        x => x.Name),
                    x => x.Children),
                new TextColumn<RepoNodeVM, string>(
                    "存储数",
                    x => x.SaveFileNodeCntString)
            }
        };
    }

    public static HierarchicalTreeDataGridSource<FileNodeVM> CreateFileSource(
        ObservableCollection<FileNodeVM> fileNodeVm)
    {
        return new HierarchicalTreeDataGridSource<FileNodeVM>(fileNodeVm)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileNodeVM>(
                    new TemplateColumn<FileNodeVM>(
                        "Name",
                        new FuncDataTemplate<FileNodeVM>((x, ns) =>
                        {
                            var textBlock = new TextBlock();
                            textBlock.Bind(
                                TextBlock.TextProperty,
                                new Avalonia.Data.Binding("Name"));
                            textBlock.Bind(
                                TextBlock.ForegroundProperty,
                                new Avalonia.Data.Binding("NameBrushes"));
                            return textBlock;
                        })),
                    x => x.Children)
            }
        };
    }
}
