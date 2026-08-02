using CommunityToolkit.Mvvm.Input;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class FolderSelectDialogViewModelTests
{
    [Fact]
    public async Task SelectFolderCommand_UsesInjectedPickerAndNormalizesTrailingSeparator()
    {
        var viewModel = new FolderSelectDialogViewModel(
            () => Task.FromResult<string?>(@"C:\Media\"),
            _ => { });

        await ((IAsyncRelayCommand)viewModel.SelectFolderCommand).ExecuteAsync(null);

        Assert.Equal(@"C:\Media", viewModel.SelectedPath);
    }

    [Fact]
    public async Task SelectFolderCommand_KeepsCurrentPathWhenPickerIsCancelled()
    {
        var viewModel = new FolderSelectDialogViewModel(
            () => Task.FromResult<string?>(null),
            _ => { })
        {
            SelectedPath = @"C:\Existing",
        };

        await ((IAsyncRelayCommand)viewModel.SelectFolderCommand).ExecuteAsync(null);

        Assert.Equal(@"C:\Existing", viewModel.SelectedPath);
    }

    [Fact]
    public void ConfirmCommand_ReturnsTrimmedSelectionThroughInjectedCloseAction()
    {
        (string Path, string Tag)? result = null;
        var viewModel = new FolderSelectDialogViewModel(
            () => Task.FromResult<string?>(null),
            selection => result = selection)
        {
            SelectedPath = "  C:\\Media  ",
            TagText = "  Archive  ",
        };

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal((@"C:\Media", "Archive"), result);
    }
}
