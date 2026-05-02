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
        MessageText = "以下 JSON 文件有未保存的修改。";
        foreach (var filePath in dirtyFilePaths)
            DirtyFilePaths.Add(filePath);

        SaveAndExitCommand = new RelayCommand(() =>
            Window?.Close(UnsavedChangesDialogResult.SaveAndExit));
        CancelCommand = new RelayCommand(() =>
            Window?.Close(UnsavedChangesDialogResult.Cancel));
        ExitWithoutSavingCommand = new RelayCommand(() =>
            Window?.Close(UnsavedChangesDialogResult.ExitWithoutSaving));
    }
}
