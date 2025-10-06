using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Kernel;
using HDD_Index.Messages;
using HDD_Index.Models;
using HDD_Index.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class FolderSelectDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }
    
    [Reactive]
    public string SelectedPath
    {
        get;
        set;
    }

    [Reactive]
    public string TagText
    {
        get;
        set;
    }

    public bool CanConfirm =>
        !string.IsNullOrWhiteSpace(SelectedPath)
        && !string.IsNullOrWhiteSpace(TagText);
    
    public ICommand SelectFolderCommand { get; set; }
    public ICommand ConfirmCommand { get; set; }

    public FolderSelectDialogViewModel()
    {
        SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        ConfirmCommand = new RelayCommand(Confirm);
    }
    
    private async Task SelectFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择文件夹",
        };

        var mainWindow =
            (Application.Current?.ApplicationLifetime as
                IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var result = await dialog.ShowAsync(mainWindow);
        if (!string.IsNullOrEmpty(result))
        {
            SelectedPath = result;
        }
    }

    private void Confirm()
    {
        var ret = new ValueTuple<string, string>(SelectedPath, TagText);

        this.Window?.Close(ret);
    }
}