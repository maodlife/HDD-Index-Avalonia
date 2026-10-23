using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class DialogSafetyTests
{
    [Fact]
    public void Confirmation_PreservesExplicitDestructiveActionAndFullDetails()
    {
        var details = string.Join(Environment.NewLine, Enumerable.Repeat("Archive 01/很长的路径/optional.ass", 100));
        var vm = new ConfirmMessageDialogViewModel(details, "关联不符合新策略", "切换并移除关联", true);

        Assert.Equal(details, vm.MessageText);
        Assert.Equal("关联不符合新策略", vm.Heading);
        Assert.Equal("切换并移除关联", vm.ConfirmLabel);
        Assert.True(vm.IsDestructive);
    }

    [Fact]
    public void Confirmation_DefaultDoesNotImplyDestructiveAction()
    {
        var vm = new ConfirmMessageDialogViewModel("详情");
        Assert.False(vm.IsDestructive);
        Assert.Equal("继续", vm.ConfirmLabel);
    }

    [Fact]
    public void Unlink_EmptyOptionsCannotBeConfirmed()
    {
        var vm = new AbandonDeclareHoldingDialogViewModel(["", " "]);
        Assert.Empty(vm.Options);
        Assert.False(vm.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public void Unlink_SelectionUpdatesConfirmationAvailabilityWithoutChangingPaths()
    {
        var vm = new AbandonDeclareHoldingDialogViewModel(["Repository/动画", "Repository/动画", "Repository/音乐"]);
        var changes = 0;
        vm.ConfirmCommand.CanExecuteChanged += (_, _) => changes++;

        Assert.Equal(2, vm.Options.Count);
        Assert.True(vm.ConfirmCommand.CanExecute(null));
        foreach (var option in vm.Options)
            option.IsSelected = false;
        Assert.False(vm.ConfirmCommand.CanExecute(null));
        vm.Options[1].IsSelected = true;
        Assert.True(vm.ConfirmCommand.CanExecute(null));
        Assert.True(changes >= 3);
        Assert.Equal("Repository/音乐", vm.Options[1].RepoNodePath);
    }

    [Fact]
    public void UnsavedFiles_KeepsEveryPathAndReportsActualCount()
    {
        string[] files = ["/data/RepoTreeData.json", "/data/Archive 01.json"];
        var vm = new UnsavedChangesDialogViewModel(files);
        Assert.Equal(files, vm.DirtyFilePaths);
        Assert.Equal("有 2 个索引文件尚未保存。", vm.MessageText);
        Assert.Equal(0, (int)UnsavedChangesDialogResult.Cancel);
    }

    [Fact]
    public void ModalMessage_PreservesErrorDetailsAndHeading()
    {
        var vm = new MessageDialogViewModel("无法保存：磁盘空间不足", "保存失败");
        Assert.Equal("保存失败", vm.Heading);
        Assert.Equal("无法保存：磁盘空间不足", vm.MessageText);
    }
}
