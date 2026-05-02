using System.Reactive;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class RenameRepoNodeDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive]
    public string NameText { get; set; } = string.Empty;

    public bool CanConfirm => !string.IsNullOrWhiteSpace(NameText);

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public RenameRepoNodeDialogViewModel(string? initialName = null)
    {
        NameText = initialName ?? string.Empty;

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    private void Confirm()
    {
        var name = NameText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            // 视为取消
            Window?.Close(null);
            return;
        }

        Window?.Close(name);
    }

    private void Cancel()
    {
        Window?.Close(null);
    }
}

