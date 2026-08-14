using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Views;

namespace HDD_Index.Adapters;

public sealed class AvaloniaFileTreeInteraction : IFileTreeInteraction
{
    private readonly Window _owner;

    public AvaloniaFileTreeInteraction(Window owner)
    {
        _owner = owner;
    }

    public async Task<bool> ConfirmDeleteAsync(string targetName)
    {
        var dialog = new DeleteConfirmDialog(targetName)
        {
            Title = "确认删除",
            Width = 400,
            Height = 150,
        };
        return await dialog.ShowDialog<bool>(_owner);
    }

    public async Task<NewFileTreeSelection?> RequestNewFileTreeAsync()
    {
        var dialog = new FolderSelectDialog(_owner)
        {
            Title = "选择文件夹并填写标签",
            Width = 450,
            Height = 150,
        };
        var result = await dialog.ShowDialog<(string? Path, string? Tag)?>(_owner);
        return result is { Path: not null, Tag: not null }
            ? new NewFileTreeSelection(result.Value.Path, result.Value.Tag)
            : null;
    }

    public async Task<string?> RequestLocalFolderPathAsync(
        string diskLabel,
        string currentPath)
    {
        if (!_owner.StorageProvider.CanPickFolder)
            return null;

        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = $"为 {diskLabel} 选择新的本地文件夹",
                AllowMultiple = false,
            });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
