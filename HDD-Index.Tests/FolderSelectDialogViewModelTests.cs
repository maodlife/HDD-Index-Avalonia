using CommunityToolkit.Mvvm.Input;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class FolderSelectDialogViewModelTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task SelectFolderCommand_UsesInjectedPickerAndNormalizesTrailingSeparator(
        int separatorCount)
    {
        var folderPath = Path.Combine(Path.GetTempPath(), "Media");
        var viewModel = new FolderSelectDialogViewModel(
            () => Task.FromResult<string?>(
                folderPath + new string(Path.DirectorySeparatorChar, separatorCount)),
            _ => { });

        await ((IAsyncRelayCommand)viewModel.SelectFolderCommand).ExecuteAsync(null);

        Assert.Equal(folderPath, viewModel.SelectedPath);
    }

    [Fact]
    public async Task SelectFolderCommand_PreservesFileSystemRoot()
    {
        var rootPath = Path.GetPathRoot(Path.GetTempPath())!;
        var viewModel = new FolderSelectDialogViewModel(
            () => Task.FromResult<string?>(rootPath),
            _ => { });

        await ((IAsyncRelayCommand)viewModel.SelectFolderCommand).ExecuteAsync(null);

        Assert.Equal(rootPath, viewModel.SelectedPath);
        Assert.Equal(rootPath, Path.GetPathRoot(viewModel.SelectedPath));
    }

    [Fact]
    public async Task SelectFolderCommand_RespectsPlatformMeaningOfBackslash()
    {
        var folderPath = Path.Combine(Path.GetTempPath(), @"Media\");
        var viewModel = new FolderSelectDialogViewModel(
            () => Task.FromResult<string?>(folderPath),
            _ => { });

        await ((IAsyncRelayCommand)viewModel.SelectFolderCommand).ExecuteAsync(null);

        var expectedPath = OperatingSystem.IsWindows()
            ? Path.Combine(Path.GetTempPath(), "Media")
            : folderPath;
        Assert.Equal(expectedPath, viewModel.SelectedPath);
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
