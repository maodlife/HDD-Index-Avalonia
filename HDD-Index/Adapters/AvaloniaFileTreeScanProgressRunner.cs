using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Application.FileScanning;
using HDD_Index.ViewModels;
using HDD_Index.Views;

namespace HDD_Index.Adapters;

public sealed class AvaloniaFileTreeScanProgressRunner : IFileTreeScanProgressRunner
{
    private readonly Window _owner;

    public AvaloniaFileTreeScanProgressRunner(Window owner)
    {
        _owner = owner;
    }

    public async Task<FileTreeScanExecutionResult<T>> RunAsync<T>(
        Func<IProgress<FileTreeScanProgress>, CancellationToken, T> scan)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var progressViewModel =
            new FileTreeScanProgressDialogViewModel(cancellationTokenSource);
        var progressDialog = new FileTreeScanProgressDialog(progressViewModel)
        {
            Title = "正在读取",
            Width = 520,
            Height = 220,
        };
        var progress = new Progress<FileTreeScanProgress>(
            progressViewModel.UpdateProgress);

        progressDialog.Show(_owner);
        try
        {
            var result = await Task.Run(
                () => scan(progress, cancellationTokenSource.Token),
                cancellationTokenSource.Token);
            return new FileTreeScanExecutionResult<T>(
                result,
                IsCancelled: false,
                Error: null);
        }
        catch (OperationCanceledException)
        {
            return new FileTreeScanExecutionResult<T>(
                default,
                IsCancelled: true,
                Error: null);
        }
        catch (Exception ex)
        {
            return new FileTreeScanExecutionResult<T>(
                default,
                IsCancelled: false,
                Error: ex);
        }
        finally
        {
            progressDialog.CloseAfterScan();
        }
    }
}
