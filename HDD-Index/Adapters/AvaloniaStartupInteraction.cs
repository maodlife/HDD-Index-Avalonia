using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HDD_Index.Application.ExternalInteractions;

namespace HDD_Index.Adapters;

public sealed class AvaloniaStartupInteraction : IStartupInteraction
{
    private readonly Window _owner;

    public AvaloniaStartupInteraction(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> SelectDataDirectoryAsync(string title)
    {
        if (!_owner.StorageProvider.CanPickFolder)
            return null;

        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
