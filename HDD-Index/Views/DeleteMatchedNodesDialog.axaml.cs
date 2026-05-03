using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class DeleteMatchedNodesDialog : Window
{
    public DeleteMatchedNodesDialog()
    {
        InitializeComponent();
        var vm = new DeleteMatchedNodesDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public DeleteMatchedNodesDialog(string messageText, string pathsText)
    {
        InitializeComponent();
        var vm = new DeleteMatchedNodesDialogViewModel(messageText, pathsText);
        DataContext = vm;
        vm.Window = this;
    }
}
