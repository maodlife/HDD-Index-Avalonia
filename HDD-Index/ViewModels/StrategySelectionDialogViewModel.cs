using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using HDD_Index.Models;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class StrategySelectionDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    public ObservableCollection<DeclareHoldingStrategyOption> StrategyOptions { get; }

    [Reactive] public DeclareHoldingStrategyOption? SelectedStrategyOption { get; set; }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public StrategySelectionDialogViewModel()
        : this(DeclareHoldingStrategyFactory.GetAllOptions())
    {
    }

    public StrategySelectionDialogViewModel(
        IEnumerable<DeclareHoldingStrategyOption> strategyOptions)
    {
        StrategyOptions = new ObservableCollection<DeclareHoldingStrategyOption>(
            strategyOptions);
        SelectedStrategyOption = StrategyOptions.FirstOrDefault();
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    private void Confirm()
    {
        Window?.Close(SelectedStrategyOption?.Type);
    }

    private void Cancel()
    {
        Window?.Close(null);
    }
}
