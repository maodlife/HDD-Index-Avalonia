using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class FolderSelectDialog : Window
{
    public FolderSelectDialog()
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        DataContext = new FolderSelectDialogViewModel();
    }

    public FolderSelectDialog(Window owner)
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        DataContext = new FolderSelectDialogViewModel(
            () => SelectFolderAsync(owner),
            result => Close(result));
    }

    private static async Task<string?> SelectFolderAsync(Window owner)
    {
        if (!owner.StorageProvider.CanPickFolder)
            return null;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "选择文件夹",
                AllowMultiple = false,
            });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
