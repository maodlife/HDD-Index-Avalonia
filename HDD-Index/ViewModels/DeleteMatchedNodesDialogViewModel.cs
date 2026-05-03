using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class DeleteMatchedNodesDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive] public string MessageText { get; set; }
    [Reactive] public string PathsText { get; set; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public DeleteMatchedNodesDialogViewModel()
        : this(string.Empty, string.Empty)
    {
    }

    public DeleteMatchedNodesDialogViewModel(string messageText, string pathsText)
    {
        MessageText = messageText;
        PathsText = pathsText;
        CancelCommand = ReactiveCommand.Create(() => Window?.Close(false));
        ConfirmCommand = ReactiveCommand.Create(() => Window?.Close(true));
    }
}
