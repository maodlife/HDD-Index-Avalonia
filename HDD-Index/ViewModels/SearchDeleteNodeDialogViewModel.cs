using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class SearchDeleteNodeDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive]
    public string SearchText { get; set; } = string.Empty;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public SearchDeleteNodeDialogViewModel()
    {
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    private void Confirm()
    {
        var searchText = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            Window?.Close(null);
            return;
        }

        Window?.Close(searchText);
    }

    private void Cancel()
    {
        Window?.Close(null);
    }
}
