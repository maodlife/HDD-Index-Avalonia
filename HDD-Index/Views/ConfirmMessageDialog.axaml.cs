using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class ConfirmMessageDialog : Window
{
    public ConfirmMessageDialog()
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new ConfirmMessageDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public ConfirmMessageDialog(
        string messageText,
        string heading = "请确认操作",
        string confirmLabel = "继续",
        bool isDestructive = false)
    {
        InitializeComponent();
        Behaviors.DialogFocus.Attach(this);
        var vm = new ConfirmMessageDialogViewModel(messageText, heading, confirmLabel, isDestructive);
        DataContext = vm;
        vm.Window = this;
    }
}
