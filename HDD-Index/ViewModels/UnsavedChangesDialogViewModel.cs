using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public enum UnsavedChangesDialogResult
{
    Cancel = 0,
    SaveAndExit = 1,
    ExitWithoutSaving = 2
}

public class UnsavedChangesDialogViewModel : ViewModelBase
{
    public Window? Window { get; set; }

    [Reactive] public string MessageText { get; set; }

    public ObservableCollection<string> DirtyFilePaths { get; } = new();
    public ICommand SaveAndExitCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExitWithoutSavingCommand { get; }

    public UnsavedChangesDialogViewModel()
        : this([])
    {
    }

    public UnsavedChangesDialogViewModel(IEnumerable<string> dirtyFilePaths)
    {
        foreach (var filePath in dirtyFilePaths)
            DirtyFilePaths.Add(filePath);
        MessageText = $"有 {DirtyFilePaths.Count} 个索引文件尚未保存。";

        SaveAndExitCommand = new RelayCommand(() =>
            Window?.Close(UnsavedChangesDialogResult.SaveAndExit));
        CancelCommand = new RelayCommand(() =>
            Window?.Close(UnsavedChangesDialogResult.Cancel));
        ExitWithoutSavingCommand = new RelayCommand(() =>
            Window?.Close(UnsavedChangesDialogResult.ExitWithoutSaving));
    }
}
