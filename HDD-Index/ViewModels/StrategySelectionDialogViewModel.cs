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
        IEnumerable<DeclareHoldingStrategyOption> strategyOptions,
        bool includeClearOption = false,
        DeclareHoldingStrategyType? selectedStrategyType = null)
    {
        var options = strategyOptions.ToList();
        if (includeClearOption)
            options.Insert(0, new DeclareHoldingStrategyOption(null, "清空"));

        StrategyOptions = new ObservableCollection<DeclareHoldingStrategyOption>(options);
        SelectedStrategyOption = StrategyOptions.FirstOrDefault(
            x => x.Type == selectedStrategyType)
            ?? StrategyOptions.FirstOrDefault();
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    private void Confirm()
    {
        if (SelectedStrategyOption == null)
        {
            Window?.Close(null);
            return;
        }

        Window?.Close(new StrategySelectionDialogResult(
            SelectedStrategyOption.Type));
    }

    private void Cancel()
    {
        Window?.Close(null);
    }
}

public sealed record StrategySelectionDialogResult(
    DeclareHoldingStrategyType? StrategyType);
