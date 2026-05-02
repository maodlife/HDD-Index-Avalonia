using System.Collections.Generic;
using Avalonia.Controls;
using HDD_Index.Models;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class StrategySelectionDialog : Window
{
    public StrategySelectionDialog()
    {
        InitializeComponent();
        var vm = new StrategySelectionDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public StrategySelectionDialog(
        IEnumerable<DeclareHoldingStrategyOption> strategyOptions)
    {
        InitializeComponent();
        var vm = new StrategySelectionDialogViewModel(strategyOptions);
        DataContext = vm;
        vm.Window = this;
    }
}
