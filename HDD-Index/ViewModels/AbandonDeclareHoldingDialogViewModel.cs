using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class AbandonDeclareHoldingDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    public ObservableCollection<AbandonDeclareHoldingOption> Options { get; }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public AbandonDeclareHoldingDialogViewModel()
        : this(Enumerable.Empty<string>())
    {
    }

    public AbandonDeclareHoldingDialogViewModel(IEnumerable<string> repoNodePaths)
    {
        Options = new ObservableCollection<AbandonDeclareHoldingOption>(
            repoNodePaths
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Select(x => new AbandonDeclareHoldingOption(x, isSelected: true)));
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    private void Confirm()
    {
        Window?.Close(Options
            .Where(x => x.IsSelected)
            .Select(x => x.RepoNodePath)
            .ToList());
    }

    private void Cancel()
    {
        Window?.Close(null);
    }
}

public class AbandonDeclareHoldingOption : ViewModelBase
{
    public string RepoNodePath { get; }

    [Reactive] public bool IsSelected { get; set; }

    public AbandonDeclareHoldingOption(string repoNodePath, bool isSelected)
    {
        RepoNodePath = repoNodePath;
        IsSelected = isSelected;
    }
}
