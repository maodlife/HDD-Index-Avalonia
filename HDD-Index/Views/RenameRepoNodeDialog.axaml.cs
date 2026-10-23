using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class RenameRepoNodeDialog : Window
{
    public RenameRepoNodeDialog()
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new RenameRepoNodeDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public RenameRepoNodeDialog(string? initialName)
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new RenameRepoNodeDialogViewModel(initialName);
        DataContext = vm;
        vm.Window = this;
    }
}

