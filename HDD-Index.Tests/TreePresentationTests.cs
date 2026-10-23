using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class TreePresentationTests
{
    [Fact]
    public void RelationshipRefresh_NotifiesLinkedIndicatorForAddAndRemove()
    {
        var fileNode = TestTreeFactory.File("Episode.mkv");
        var projection = new TreeProjection();
        var vm = projection.CreateFileTree(fileNode);
        var raisedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        Assert.False(vm.HasDeclarations);
        fileNode.DeclareRepoNodeDatas.Add(new DeclareRepoNodeData
        {
            RepoNodePath = "Repository/Animation/Episode.mkv",
        });
        vm.Refresh(TreeNodePresentation.Relationships);

        Assert.True(vm.HasDeclarations);
        Assert.Contains(nameof(FileNodeVM.HasDeclarations), raisedProperties);

        raisedProperties.Clear();
        fileNode.DeclareRepoNodeDatas.Clear();
        vm.Refresh(TreeNodePresentation.Relationships);

        Assert.False(vm.HasDeclarations);
        Assert.Contains(nameof(FileNodeVM.HasDeclarations), raisedProperties);
    }

    [Fact]
    public void RepositoryColumns_PreserveSingleSelectionAndUseLocalizedHeaders()
    {
        var root = TestTreeFactory.Repo("Repository");
        var source = TreeDataGridSourceFactory.CreateRepoSource(RepoNodeVM.Create(root));

        Assert.Equal(3, source.Columns.Count);
        Assert.Equal("名称", source.Columns[0].Header);
        Assert.Equal("存储数", source.Columns[1].Header);
        Assert.Equal("策略", source.Columns[2].Header);
        Assert.True(source.RowSelection?.SingleSelect);
    }

    [Fact]
    public void DiskColumns_UseSingleSelectionForLinkActions()
    {
        var root = FileNodeVM.Create(TestTreeFactory.File("Archive 01"));
        var source = TreeDataGridSourceFactory.CreateFileSource(new() { root });

        Assert.Single(source.Columns);
        Assert.Equal("名称", source.Columns[0].Header);
        Assert.True(source.RowSelection?.SingleSelect);
    }
}
