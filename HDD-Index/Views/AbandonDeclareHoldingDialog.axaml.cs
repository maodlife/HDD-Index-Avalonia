using System.Collections.Generic;
using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class AbandonDeclareHoldingDialog : Window
{
    public AbandonDeclareHoldingDialog()
    {
        InitializeComponent();
        var vm = new AbandonDeclareHoldingDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public AbandonDeclareHoldingDialog(IEnumerable<string> repoNodePaths)
    {
        InitializeComponent();
        var vm = new AbandonDeclareHoldingDialogViewModel(repoNodePaths);
        DataContext = vm;
        vm.Window = this;
    }
}
