using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class RepositoryEditorViewModel : ViewModelBase
{
    [Reactive] public bool AutoJumpToSaveFileNode { get; set; }

    [Reactive] public bool AutoJumpToDeclareRepoNode { get; set; }

    public ReactiveCommand<object, Unit> LogNodePathCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<object, Unit> CreateChildFolderCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<object, Unit> RenameRepoNodeCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<object, Unit> DeleteRepoNodeCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<Unit, Unit> JumpToCurrSelectSaveFileNodeCommand { get; set; }
        = ReactiveCommand.Create(() => { });

    public ReactiveCommand<object, Unit> JumpToDeclareRepoNodeCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<object, Unit> DeclareSelectedRepoNodeCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<object, Unit> AbandonDeclareHoldingCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<object, Unit> ChangeDeclareHoldingStrategyCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });

    public ReactiveCommand<Unit, Unit> OpenCurrentFileDataFolderCommand { get; set; }
        = ReactiveCommand.Create(() => { });

    public ReactiveCommand<object, Unit> OpenFileNodeInFolderCommand { get; set; }
        = ReactiveCommand.Create<object>(_ => { });
}
