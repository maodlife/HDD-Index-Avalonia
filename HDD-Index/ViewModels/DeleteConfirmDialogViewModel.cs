using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class DeleteConfirmDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive] public string MessageText { get; set; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public DeleteConfirmDialogViewModel()
    {
        MessageText = "将移除所选节点及其子节点。";
        CancelCommand = ReactiveCommand.Create(() => Window?.Close(false));
        ConfirmCommand = ReactiveCommand.Create(() => Window?.Close(true));
    }

    public DeleteConfirmDialogViewModel(string targetName)
    {
        MessageText = $"将移除「{targetName}」及其子节点。";
        CancelCommand = ReactiveCommand.Create(() => Window?.Close(false));
        ConfirmCommand = ReactiveCommand.Create(() => Window?.Close(true));
    }
}
