using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.Startup;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public sealed class StartupViewModel : ViewModelBase
{
    private readonly IApplicationStartupService _startupService;
    private readonly IStartupInteraction _startupInteraction;
    private readonly Action<ApplicationStartupResult?> _complete;
    private ApplicationStartupResult _result;

    [Reactive] public string Heading { get; private set; } = string.Empty;

    [Reactive] public string Summary { get; private set; } = string.Empty;

    [Reactive] public string Details { get; private set; } = string.Empty;

    [Reactive] public bool CanCreate { get; private set; }

    [Reactive] public bool CanRepairDataDirectory { get; private set; }

    [Reactive] public bool IsBusy { get; private set; }

    public ICommand CreateCommand { get; }

    public ICommand RepairDataDirectoryCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand ExitCommand { get; }

    public StartupViewModel(
        ApplicationStartupResult result,
        IApplicationStartupService startupService,
        IStartupInteraction startupInteraction,
        Action<ApplicationStartupResult?> complete)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
        _startupService = startupService
                          ?? throw new ArgumentNullException(nameof(startupService));
        _startupInteraction = startupInteraction
                              ?? throw new ArgumentNullException(nameof(startupInteraction));
        _complete = complete ?? throw new ArgumentNullException(nameof(complete));
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        RepairDataDirectoryCommand = new AsyncRelayCommand(RepairDataDirectoryAsync);
        RetryCommand = new RelayCommand(Retry);
        ExitCommand = new RelayCommand(() => _complete(null));
        ApplyResult(result);
    }

    private async Task CreateAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var selectedPath = await _startupInteraction.SelectDataDirectoryAsync(
                "选择保存 HDD Index 数据的文件夹");
            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            HandleResult(_startupService.CreateDefault(selectedPath));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RepairDataDirectoryAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var selectedPath = await _startupInteraction.SelectDataDirectoryAsync(
                "选择包含 Repository 和磁盘索引的数据文件夹");
            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            HandleResult(_startupService.RepairDataDirectory(
                _result.ConfigFilePath,
                selectedPath));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Retry()
    {
        if (IsBusy)
            return;

        HandleResult(_startupService.Load(_result.ConfigFilePath));
    }

    private void HandleResult(ApplicationStartupResult result)
    {
        if (result.State == ApplicationStartupState.Ready)
        {
            _complete(result);
            return;
        }

        ApplyResult(result);
    }

    private void ApplyResult(ApplicationStartupResult result)
    {
        _result = result;
        var issue = result.BlockingIssue;
        CanCreate = result.State == ApplicationStartupState.FirstRun
                    || issue?.Kind is SessionLoadIssueKind.InitializationConflict
                        or SessionLoadIssueKind.InitializationFailed;
        CanRepairDataDirectory = issue?.Kind is
            SessionLoadIssueKind.DataDirectoryMissing
            or SessionLoadIssueKind.DataDirectoryUnreadable
            or SessionLoadIssueKind.RepositoryMissing
            or SessionLoadIssueKind.RepositoryInvalid
            or SessionLoadIssueKind.RepositoryUnreadable;

        if (result.State == ApplicationStartupState.FirstRun)
        {
            Heading = "欢迎使用 HDD Index";
            Summary = "这是首次启动。选择一个数据文件夹后，应用会创建初始配置和空 Repository。";
        }
        else
        {
            Heading = "HDD Index 无法完成启动";
            Summary = CanRepairDataDirectory
                ? "配置仍然可读，但它指向的数据位置不可用。你可以选择迁移后的数据文件夹。"
                : "请根据下面的诊断修复文件，然后重试。应用不会自动覆盖现有数据。";
        }

        Details = issue == null
            ? $"配置文件：{result.ConfigFilePath}"
            : $"{issue.Message}{Environment.NewLine}{Environment.NewLine}位置：{issue.FilePath}";
        this.RaisePropertyChanged(nameof(CanCreate));
        this.RaisePropertyChanged(nameof(CanRepairDataDirectory));
    }
}
