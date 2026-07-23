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
    } = string.Empty;

    [Reactive]
    public string TagText
    {
        get;
        set;
    } = string.Empty;

    public bool CanConfirm =>
        !string.IsNullOrWhiteSpace(SelectedPath)
        && !string.IsNullOrWhiteSpace(TagText);
    
    public ICommand SelectFolderCommand { get; set; }
    public ICommand ConfirmCommand { get; set; }

    public FolderSelectDialogViewModel()
    {
        SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        ConfirmCommand = new RelayCommand(Confirm);
        this.WhenAnyValue(x => x.SelectedPath, x => x.TagText)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(CanConfirm)));
    }
    
    private async Task SelectFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择文件夹",
        };

        var owner = Window ??
            (Avalonia.Application.Current?.ApplicationLifetime as
                IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner == null)
            return;

        var result = await dialog.ShowAsync(owner);
        if (!string.IsNullOrEmpty(result))
        {
            var path = result.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            SelectedPath = path;
        }
    }

    private void Confirm()
    {
        if (!CanConfirm)
            return;

        var ret = new ValueTuple<string, string>(
            SelectedPath.Trim(),
            TagText.Trim());

        this.Window?.Close(ret);
    }
}
