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

    public FolderSelectDialogViewModel()
    {
        SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
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
        // 将结果通过 Window.DataContext 回传
        var window =
            (Application.Current?.ApplicationLifetime as
                IClassicDesktopStyleApplicationLifetime)?
            .MainWindow?.OwnedWindows
            .FirstOrDefault(w => w.DataContext == this);

        window?.Close((SelectedPath, TagText));
    }
}