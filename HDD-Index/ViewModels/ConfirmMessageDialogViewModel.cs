using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class ConfirmMessageDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive] public string MessageText { get; set; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public ConfirmMessageDialogViewModel()
        : this(string.Empty)
    {
    }

    public ConfirmMessageDialogViewModel(string messageText)
    {
        MessageText = messageText;
        CancelCommand = ReactiveCommand.Create(() => Window?.Close(false));
        ConfirmCommand = ReactiveCommand.Create(() => Window?.Close(true));
    }
}
