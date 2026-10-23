using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _informationTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool _isClosingConfirmed;
    private bool _isShowingUnsavedChangesDialog;

    public MainWindow()
    {
        InitializeComponent();
        _informationTimer.Tick += (_, _) => DismissInformation();
        Closed += (_, _) => _informationTimer.Stop();
    }

    public void ShowInformation(string message)
    {
        _informationTimer.Stop();
        InformationToastText.Text = message;
        InformationToast.IsVisible = true;
        _informationTimer.Start();
    }

    private void OnDismissInformation(object? sender, RoutedEventArgs e) => DismissInformation();

    private void DismissInformation()
    {
        _informationTimer.Stop();
        InformationToast.IsVisible = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            var field = e.Key == Key.L ? RepositoryAddress
                : e.Key == Key.F ? RepositorySearch : null;
            if (field != null)
            {
                field.Focus();
                field.SelectAll();
                e.Handled = true;
            }
        }

        base.OnKeyDown(e);
    }

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.SelectRepositoryCommand.Execute().Subscribe();
            vm.RepoBrowser.RepoNodePathString = RepositoryAddress.Text ?? string.Empty;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            RepositoryAddress.Text = vm.RepoBrowser.RepoNodePathString;
            RepositoryAddress.SelectAll();
            e.Handled = true;
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Avalonia.Application.Current is { } application)
            NativeMenu.SetMenu(application, NativeMenu.GetMenu(this));
        if (DataContext is MainWindowViewModel vm)
            await vm.ShowStartupWarningsAsync();
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
