using HDD_Index.Application.TreeEditing;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class TreeProjectionTests
{
    [Fact]
    public void ApplyPresentationChange_RefreshesVmWithoutCopyingModelData()
    {
        var repoNode = TestTreeFactory.Repo("Movies");
        var projection = new TreeProjection();
        var vm = projection.CreateRepoTree(repoNode);
        var raisedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        repoNode.Name = "Films";
        repoNode.SaveFileNodeDatas.Add(new SaveFileNodeData
        {
            DiskLabel = "DiskA",
            FileNodePath = "Disk/Films"
        });
        var changes = new TreeChangeCollector();
        changes.Refresh(repoNode, TreeNodePresentation.Name | TreeNodePresentation.Relationships);
        projection.Apply(changes.Build());

        Assert.Equal("Films", vm.Name);
        Assert.Equal(1, vm.SaveFileNodeCnt);
        Assert.Same(repoNode.SaveFileNodeDatas, vm.SaveFileNodeDatas);
        Assert.Contains(nameof(RepoNodeVM.Name), raisedProperties);
        Assert.Contains(nameof(RepoNodeVM.SaveFileNodeCnt), raisedProperties);
    }

    [Fact]
    public void ApplyNodeAddedAndRemoved_UpdatesOnlyProjectionStructure()
    {
        var root = TestTreeFactory.Repo("Root");
        var projection = new TreeProjection();
        var rootVm = projection.CreateRepoTree(root);
        var child = TestTreeFactory.Repo("Child");
        child.Parent = root;
        root.Children.Add(child);
        var addChanges = new TreeChangeCollector();
        addChanges.AddNode(root, child, 0);

        projection.Apply(addChanges.Build());

        Assert.Single(rootVm.Children);
        Assert.Same(child, rootVm.Children[0].RepoNode);

        root.Children.Remove(child);
        var removeChanges = new TreeChangeCollector();
        removeChanges.RemoveNode(root, child);
        projection.Apply(removeChanges.Build());

        Assert.Empty(rootVm.Children);
        Assert.False(projection.TryGetRepoNodeVm(child, out _));
    }

    [Fact]
    public void ApplySubtreeReplacement_PreservesRootVmAndRebuildsDescendants()
    {
        var oldChild = TestTreeFactory.File("Old");
        var root = TestTreeFactory.File("Disk", oldChild);
        var projection = new TreeProjection();
        var rootVm = projection.CreateFileTree(root);
        var newChild = TestTreeFactory.File("New");
        newChild.Parent = root;
        root.Children.Clear();
        root.Children.Add(newChild);
        var changes = new TreeChangeCollector();
        changes.ReplaceSubtree(root);

        projection.Apply(changes.Build());

        Assert.Same(rootVm, projection.GetFileNodeVm(root));
        Assert.Single(rootVm.Children);
        Assert.Same(newChild, rootVm.Children[0].FileNode);
        Assert.False(projection.TryGetFileNodeVm(oldChild, out _));
    }
}
