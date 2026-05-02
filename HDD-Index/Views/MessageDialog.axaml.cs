using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
        var vm = new MessageDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public MessageDialog(string messageText)
    {
        InitializeComponent();
        var vm = new MessageDialogViewModel(messageText);
        DataContext = vm;
        vm.Window = this;
    }
}
