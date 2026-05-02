using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class MessageDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive] public string MessageText { get; set; }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public MessageDialogViewModel()
        : this(string.Empty)
    {
    }

    public MessageDialogViewModel(string messageText)
    {
        MessageText = messageText;
        ConfirmCommand = ReactiveCommand.Create(() => Window?.Close());
    }
}
