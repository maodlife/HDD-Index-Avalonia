using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class MessageDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive] public string MessageText { get; set; }

    public string Heading { get; }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public MessageDialogViewModel()
        : this(string.Empty)
    {
    }

    public MessageDialogViewModel(string messageText, string heading = "操作未完成")
    {
        MessageText = messageText;
        Heading = heading;
        ConfirmCommand = ReactiveCommand.Create(() => Window?.Close());
    }
}
