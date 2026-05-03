using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class SearchDeleteNodeDialog : Window
{
    public SearchDeleteNodeDialog()
    {
        InitializeComponent();
        var vm = new SearchDeleteNodeDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }
}
