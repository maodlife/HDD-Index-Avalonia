using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog()
    {
        InitializeComponent();
        var vm = new DeleteConfirmDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public DeleteConfirmDialog(string targetName)
    {
        InitializeComponent();
        var vm = new DeleteConfirmDialogViewModel(targetName);
        DataContext = vm;
        vm.Window = this;
    }
}