using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class FolderSelectDialogViewModel : ViewModelBase
{
    private readonly Func<Task<string?>> _selectFolder;
    private readonly Action<(string Path, string Tag)?> _close;

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
        : this(
            () => Task.FromResult<string?>(null),
            _ => { })
    {
    }

    public FolderSelectDialogViewModel(
        Func<Task<string?>> selectFolder,
        Action<(string Path, string Tag)?> close)
    {
        _selectFolder = selectFolder;
        _close = close;
        SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        ConfirmCommand = new RelayCommand(Confirm);
        this.WhenAnyValue(x => x.SelectedPath, x => x.TagText)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(CanConfirm)));
    }

    private async Task SelectFolderAsync()
    {
        var localPath = await _selectFolder();
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        SelectedPath = localPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private void Confirm()
    {
        if (!CanConfirm)
            return;

        _close((SelectedPath.Trim(), TagText.Trim()));
    }
}
