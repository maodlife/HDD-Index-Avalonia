using Avalonia.Controls;
using HDD_Index.ViewModels;

namespace HDD_Index.Views;

public partial class FileTreeScanProgressDialog : Window
{
    private readonly FileTreeScanProgressDialogViewModel _viewModel;
    private bool _isCompleting;

    public FileTreeScanProgressDialog()
        : this(new FileTreeScanProgressDialogViewModel())
    {
    }

    public FileTreeScanProgressDialog(
        FileTreeScanProgressDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closing += (_, _) =>
        {
            if (!_isCompleting)
                _viewModel.RequestCancel();
        };
    }

    public void CloseAfterScan()
    {
        _isCompleting = true;
        Close();
    }
}
