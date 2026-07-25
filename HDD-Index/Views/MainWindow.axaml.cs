using System;
using Avalonia;
using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class MainWindow : Window
{
    private bool _isClosingConfirmed;
    private bool _isShowingUnsavedChangesDialog;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Avalonia.Application.Current is { } application)
            NativeMenu.SetMenu(application, NativeMenu.GetMenu(this));
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosingConfirmed
            || DataContext is not MainWindowViewModel vm
            || !vm.HasDirtyFiles)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_isShowingUnsavedChangesDialog)
            return;

        _isShowingUnsavedChangesDialog = true;
        try
        {
            var dialog = new UnsavedChangesDialog(vm.GetDirtyJsonFilePaths())
            {
                Title = "未保存的修改",
                Width = 560,
                Height = 320,
            };

            var result = await dialog.ShowDialog<UnsavedChangesDialogResult>(this);
            if (result == UnsavedChangesDialogResult.Cancel)
                return;

            if (result == UnsavedChangesDialogResult.SaveAndExit
                && !await vm.SaveDirtyFilesAsync())
            {
                return;
            }

            _isClosingConfirmed = true;
            Close();
        }
        finally
        {
            _isShowingUnsavedChangesDialog = false;
        }
    }
}
