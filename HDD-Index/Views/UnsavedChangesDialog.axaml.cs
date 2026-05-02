using System.Collections.Generic;
using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
        var vm = new UnsavedChangesDialogViewModel();
        DataContext = vm;
        vm.Window = this;
    }

    public UnsavedChangesDialog(IEnumerable<string> dirtyFilePaths)
    {
        InitializeComponent();
        var vm = new UnsavedChangesDialogViewModel(dirtyFilePaths);
        DataContext = vm;
        vm.Window = this;
    }
}
