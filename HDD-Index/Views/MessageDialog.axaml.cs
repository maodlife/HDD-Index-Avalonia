using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new MessageDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public MessageDialog(string messageText, string heading = "操作未完成")
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new MessageDialogViewModel(messageText, heading);
        DataContext = vm;
        vm.Window = this;
    }
}
