using System;
using System.Reactive;
using System.Threading;
using HDD_Index.Application.FileScanning;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class FileTreeScanProgressDialogViewModel : ViewModelBase
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    [Reactive] public string StatusText { get; set; } = "正在读取";
    [Reactive] public string CurrentPath { get; set; } = string.Empty;
    [Reactive] public double ProgressValue { get; set; }
    [Reactive] public string ProgressText { get; set; } = "0%";
    [Reactive] public bool IsCancelling { get; set; }
    [Reactive] public bool CanCancel { get; set; } = true;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public FileTreeScanProgressDialogViewModel()
        : this(new CancellationTokenSource())
    {
    }

    public FileTreeScanProgressDialogViewModel(
        CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        CancelCommand = ReactiveCommand.Create(RequestCancel);
    }

    public void UpdateProgress(FileTreeScanProgress progress)
    {
        CurrentPath = progress.CurrentPath;
        var percent = progress.TotalTopLevelEntries > 0
            ? Math.Clamp(
                progress.CompletedTopLevelEntries * 100d / progress.TotalTopLevelEntries,
                0d,
                100d)
            : 0d;
        ProgressValue = percent;
        ProgressText = $"{percent:0}%";
    }

    public void RequestCancel()
    {
        if (IsCancelling)
            return;

        IsCancelling = true;
        CanCancel = false;
        StatusText = "正在取消...";
        _cancellationTokenSource.Cancel();
    }
}
