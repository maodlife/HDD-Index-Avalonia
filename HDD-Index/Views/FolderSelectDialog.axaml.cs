using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class FolderSelectDialog : Window
{
    public FolderSelectDialog()
    {
        InitializeComponent();

        DataContext = new FolderSelectDialogViewModel();
        
        if (DataContext is FolderSelectDialogViewModel vm)
        {
            vm.Window = this;
        }
    }
}