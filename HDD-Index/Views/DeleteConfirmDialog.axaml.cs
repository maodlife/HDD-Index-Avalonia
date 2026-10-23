using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog()
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new DeleteConfirmDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public DeleteConfirmDialog(string targetName)
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new DeleteConfirmDialogViewModel(targetName);
        DataContext = vm;
        vm.Window = this;
    }
}
