using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class ConfirmMessageDialog : Window
{
    public ConfirmMessageDialog()
    {
        InitializeComponent();
        var vm = new ConfirmMessageDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public ConfirmMessageDialog(string messageText)
    {
        InitializeComponent();
        var vm = new ConfirmMessageDialogViewModel(messageText);
        DataContext = vm;
        vm.Window = this;
    }
}
