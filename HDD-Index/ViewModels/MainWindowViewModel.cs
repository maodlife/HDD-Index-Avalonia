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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using DynamicData;
using DynamicData.Kernel;
using HDD_Index.Messages;
using HDD_Index.Models;
using HDD_Index.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace HDD_Index.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private AppConfig _appConfig;

    #region Repo Data

    public RepoNode RepoNodeRoot { get; set; }
    public RepoNodeVM RepoNodeVm { get; set; }

    [Reactive]
    public HierarchicalTreeDataGridSource<RepoNodeVM> RepoNodeSource
    {
        get;
        set;
    }

    public ReactiveCommand<RepoNodeVM, Unit> RepoNodeSelectedCommand
    {
        get;
        set;
    }

    /// <summary>
    /// combobox中要显示的保存了当前repo节点的磁盘名
    /// </summary>
    public ObservableCollection<string>
        CurrRepoNodeSaveFileNodes { get; set; } =
        new ObservableCollection<string>();

    [Reactive] public string SelectedSaveFileNodeLabel { get; set; }

    [Reactive] public bool AutoJumpToSaveFileNode { get; set; } = false;

    [Reactive] public string RepoNodePathString { get; set; }

    public ReactiveCommand<string, Unit> RepoNodePathStringChangeCommand
    {
        get;
        set;
    }

    #endregion

    #region File Data

    public List<FileDataVMBundle> FileDataVmBundles { get; set; } =
        new List<FileDataVMBundle>();

    public int CurrShowFileNodeIndex { get; set; } = 0;

    public ObservableCollection<FileNodeVM> FileNodeVm { get; set; }
        = new ObservableCollection<FileNodeVM>();

    /// <summary>
    /// 当前实际用于View动态绑定的source
    /// </summary>
    [Reactive]
    public HierarchicalTreeDataGridSource<FileNodeVM> CurrFileNodeSource
    {
        get;
        set;
    }

    public ReactiveCommand<FileNodeVM, Unit> FileNodeSelectedCommand
    {
        get;
        set;
    }

    [Reactive]
    public ObservableCollection<string> DiskLabels { get; set; } =
        new ObservableCollection<string>();

    [Reactive] public string SelectedDiskLabel { get; set; }

    public ReactiveCommand<string, Unit> DiskLabelSelectedCommand { get; set; }

    [Reactive] public bool AutoJumpToDeclareRepoNode { get; set; } = false;

    public ReactiveCommand<object, Unit> LogNodePathCommand { get; set; }

    public ReactiveCommand<object, Unit> CreateChildFolderCommand { get; set; }
    
    public ReactiveCommand<object, Unit> RenameRepoNodeCommand { get; set; }
    
    public ReactiveCommand<object, Unit> DeleteRepoNodeCommand { get; set; }

    #endregion File Data

    #region View Mode Tab

    // 选择了浏览还是编辑
    [Reactive] public int ViewModeTabIndex { get; set; } = 0;

    public bool IsViewMode => ViewModeTabIndex == 0;
    public bool IsEditMode => ViewModeTabIndex == 1;

    #endregion

    #region 初始化

    public MainWindowViewModel()
    {
        InitConfig();
        InitRepoData();
        InitFileData();
        InitCommand();
    }

    private void InitConfig()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HDD-Index/config.json");
        var content = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(content);
        if (config != null)
        {
            _appConfig = config;
        }
    }

    private void InitCommand()
    {
        RepoNodePathStringChangeCommand =
            ReactiveCommand.Create<string>(OnRepoNodePathChange);

        this.WhenAnyValue(x => x.RepoNodePathString)
            .InvokeCommand(RepoNodePathStringChangeCommand);

        RepoNodeSelectedCommand = ReactiveCommand.Create<RepoNodeVM>(vm =>
        {
            OnSelectRepoNode(vm.RepoNode);
        });

        this.WhenAnyValue(x =>
                x.RepoNodeSource.RowSelection.SelectedItem)
            .Where(x => x != null)
            .InvokeCommand(RepoNodeSelectedCommand);

        FileNodeSelectedCommand = ReactiveCommand.Create<FileNodeVM>(vm =>
        {
            OnSelectFileNode(vm.FileNode);
        });

        this.WhenAnyValue(x =>
                x.CurrFileNodeSource.RowSelection.SelectedItem)
            .Where(x => x != null)
            .InvokeCommand(FileNodeSelectedCommand);

        DiskLabelSelectedCommand =
            ReactiveCommand.Create<string>(ChangeDiskLabel);

        this.WhenAnyValue(x => x.SelectedDiskLabel)
            .Where(x => x != null)
            .InvokeCommand(DiskLabelSelectedCommand);

        LogNodePathCommand = ReactiveCommand.Create<object>(LogNodePath);

        CreateChildFolderCommand = ReactiveCommand.Create<object>(CreateChildFolder);
        
        RenameRepoNodeCommand = ReactiveCommand.CreateFromTask<object>(RenameRepoNodeAsync);
        
        DeleteRepoNodeCommand = ReactiveCommand.CreateFromTask<object>(DeleteRepoNodeAsync);
    }

    private void InitRepoData()
    {
        var repoNodeFilePath = Path.Combine(_appConfig.JsonFilePath, _appConfig.RepoFileName);
        string json;
        try
        {
            json = File.ReadAllText(repoNodeFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("exception: " + ex.Message);
            return;
        }
        RepoNodeRoot = RepoNode.CreateByJson(json);

        RepoNodeVm = RepoNodeVM.Create(RepoNodeRoot);

        RepoNodeSource =
            new HierarchicalTreeDataGridSource<RepoNodeVM>(RepoNodeVm)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<RepoNodeVM>(
                        new TextColumn<RepoNodeVM, string>(
                            "Name",
                            x => x.Name),
                        x => x.Children),
                    new TextColumn<RepoNodeVM, string>(
                        "存储数",
                        x => x.SaveFileNodeCntString)
                }
            };
    }

    private void InitFileData()
    {
        var files = Directory.GetFiles(_appConfig.JsonFilePath);
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == _appConfig.RepoFileName)
                continue;
            var json = File.ReadAllText(file);
            var bundle =
                FileDataVMBundle.Create(
                    Path.GetFileNameWithoutExtension(file),
                    json);
            FileDataVmBundles.Add(bundle);
        }

        FileDataVmBundles.Sort((lhs, rhs)
            => String.Compare(
                lhs.FileData.DiskLabel,
                rhs.FileData.DiskLabel,
                StringComparison.Ordinal));

        foreach (var item in FileDataVmBundles)
        {
            DiskLabels.Add(item.FileData.DiskLabel);
        }

        // 默认显示第一个
        if (FileDataVmBundles.Count > 0)
        {
            ChangeFileNodeVM(FileDataVmBundles[0].FileNodeVm);
            SelectedDiskLabel = DiskLabels[0];
        }

        CurrFileNodeSource =
            new HierarchicalTreeDataGridSource<FileNodeVM>(FileNodeVm)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<FileNodeVM>(
                        new TemplateColumn<FileNodeVM>(
                            "Name",
                            new FuncDataTemplate<FileNodeVM>((x, ns) =>
                            {
                                var textBlock = new TextBlock();
                                textBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Name"));
                                textBlock.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("NameBrushes"));
                                return textBlock;
                            })),
                        x => x.Children)
                }
            };
    }

    private void ChangeFileNodeVM(FileNodeVM targetFileNodeVm)
    {
        FileNodeVm.Clear();
        FileNodeVm.Add(targetFileNodeVm);
    }

    #endregion

    #region 功能

    /// <summary>
    /// 可能是用户主动切换，也可能是从repo跳转时自动切换
    /// </summary>
    /// <param name="diskLabel"></param>
    private void ChangeDiskLabel(string diskLabel)
    {
        var found = FileDataVmBundles
            .Find(x => x.FileData.DiskLabel == diskLabel);
        if (found == null)
            return;
        CurrShowFileNodeIndex = FileDataVmBundles.IndexOf(found);
        SelectedDiskLabel = diskLabel;
        ChangeFileNodeVM(found.FileNodeVm);
    }

    private void OnRepoNodePathChange(string path)
    {
        // todo: 选中RowSelection前检查是否已经一致，防止循环跳转。
        var target = FindRepoNodeVmByPath(
            RepoNodeVm,
            path,
            out var indexPath);
        if (indexPath != null)
        {
            var parent = indexPath.Value.Slice(0, indexPath.Value.Count - 1);
            RepoNodeSource.Expand(parent);
            RepoNodeSource?.RowSelection?.Select(indexPath.Value);
            
            // 滚动到选中
            MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.ViewRepoTree));
            MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.EditRepoTree));
        }
        else
        {
            RepoNodeSource?.RowSelection?.Clear();
        }
    }

    private void OnSelectRepoNode(RepoNode repoNode)
    {
        RepoNodePathString = repoNode.GetPath();

        // 更新显示当前存储了当前repo node的节点
        CurrRepoNodeSaveFileNodes.Clear();
        foreach (var saveFileNodeData in repoNode.SaveFileNodeDatas)
        {
            CurrRepoNodeSaveFileNodes.Add(saveFileNodeData.DiskLabel);
        }

        // 默认选择第一个
        if (CurrRepoNodeSaveFileNodes.Count > 0)
        {
            SelectedSaveFileNodeLabel = CurrRepoNodeSaveFileNodes[0];
        }

        if ((IsViewMode || (IsEditMode && AutoJumpToSaveFileNode))
            && !CheckRepoNodeAndFileNodeIsSync())
        {
            JumpToDefaultSaveFileNode();
        }
    }

    private void OnSelectFileNode(FileNode fileNode)
    {
        if (IsViewMode || (IsEditMode && AutoJumpToDeclareRepoNode)
            && !CheckRepoNodeAndFileNodeIsSync())
        {
            // 自动选中对应的声明持有的repo node
            var repoNodePath = fileNode.DeclareRepoNodeDatas
                .FirstOrDefault()
                ?.RepoNodePath ?? string.Empty;
            var target = FindRepoNodeVmByPath(
                RepoNodeVm,
                repoNodePath,
                out var indexPath);
            if (indexPath != null)
            {
                var parent = indexPath.Value.Slice(0, indexPath.Value.Count - 1);
                RepoNodeSource.Expand(parent);
                RepoNodeSource?.RowSelection?.Select(indexPath.Value);
                
                // 滚动到选中
                MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.ViewRepoTree));
                MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.EditRepoTree));
            }
        }
    }

    public void JumpToDefaultSaveFileNode()
    {
        JumpToCurrSelectSaveFileNode();
    }

    /// <summary>
    /// 跳转到当前选择的file node
    /// </summary>
    public void JumpToCurrSelectSaveFileNode()
    {
        var selectRepoNode = RepoNodeSource
            ?.RowSelection
            ?.SelectedItem
            ?.RepoNode ?? null;
        var selectDiskLabel = SelectedSaveFileNodeLabel;
        if (selectRepoNode == null || string.IsNullOrEmpty(selectDiskLabel))
            return;
        var foundSaveData = selectRepoNode.SaveFileNodeDatas
            .Find(x => x.DiskLabel == selectDiskLabel);
        if (foundSaveData == null)
            return;
        ChangeDiskLabel(selectDiskLabel);
        var target = FindFileNodeVmByPath(
            FileDataVmBundles[CurrShowFileNodeIndex].FileNodeVm,
            foundSaveData.FileNodePath,
            out var indexPath);
        if (indexPath != null)
        {
            var parent = new IndexPath(indexPath.Value.Slice(0, indexPath.Value.Count - 1));
            CurrFileNodeSource.Expand(parent);
            CurrFileNodeSource?.RowSelection?.Select(indexPath.Value);
            
            // 滚动到选中
            MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.ViewFileTree));
            MessageBus.Current.SendMessage(new TargetTreeRowMessage(ControlNames.EditFileTree));
        }
    }

    /// <summary>
    /// 创建子文件夹
    /// </summary>
    private void CreateChildFolder(object nodeVM)
    {
        var repoNodeVM = (RepoNodeVM)nodeVM;

        // 生成唯一的文件夹名称
        string baseName = "新建文件夹";
        string folderName = baseName;
        int counter = 1;

        while (repoNodeVM.Children.Any(c => c.Name == folderName))
        {
            folderName = $"{baseName} ({counter})";
            counter++;
        }

        // 创建新的RepoNode
        var newRepoNode = new RepoNode
        {
            Name = folderName,
            IsDirectory = true
        };

        // 设置父子关系
        newRepoNode.Parent = repoNodeVM.RepoNode;
        repoNodeVM.RepoNode.Children.Add(newRepoNode);

        // 如果父节点已经被某些 FileNode 存储（声明持有），我们要去检查这些对应的 FileNode 中是否也刚好有这个同名的文件夹。
        // 如果有，那么新创建的节点也应该自动带有对应的 SaveFileNodeDatas
        if (repoNodeVM.RepoNode.SaveFileNodeDatas.Any())
        {
            foreach (var saveData in repoNodeVM.RepoNode.SaveFileNodeDatas)
            {
                var bundle = FileDataVmBundles.FirstOrDefault(b => b.FileData.DiskLabel == saveData.DiskLabel);
                if (bundle != null)
                {
                    // 获取父节点对应的 FileNode
                    var parentFileNodeVm = FindFileNodeVmByPath(bundle.FileNodeVm, saveData.FileNodePath, out _);
                    if (parentFileNodeVm != null)
                    {
                        // 寻找 FileNode 中是否有同名的子节点
                        var matchingChildFileNodeVm = parentFileNodeVm.Children.FirstOrDefault(c => c.Name == folderName);
                        if (matchingChildFileNodeVm != null)
                        {
                            // 找到了同名子文件夹，建立联系
                            var childFileNodePath = matchingChildFileNodeVm.FileNode.GetPath();
                            var newSaveData = new SaveFileNodeData
                            {
                                DiskLabel = bundle.FileData.DiskLabel,
                                FileNodePath = childFileNodePath
                            };
                            newRepoNode.SaveFileNodeDatas.Add(newSaveData);
                            
                            // 同步更新 FileNode 的声明数据
                            var newDeclareData = new DeclareRepoNodeData
                            {
                                RepoNodePath = newRepoNode.GetPath()
                            };
                            matchingChildFileNodeVm.FileNode.DeclareRepoNodeDatas.Add(newDeclareData);
                            matchingChildFileNodeVm.DeclareRepoNodeDatas.Add((DeclareRepoNodeData)newDeclareData.Clone());
                        }
                    }
                }
            }
        }

        // 创建对应的RepoNodeVM并添加到Children中
        var newRepoNodeVM = RepoNodeVM.Create(newRepoNode);
        repoNodeVM.Children.Add(newRepoNodeVM);

        // 我们在上面可能为 newRepoNode 添加了新的 SaveFileNodeDatas，所以需要在这里把它同步进 newRepoNodeVM 中
        newRepoNodeVM.SaveFileNodeDatas.Clear();
        foreach (var data in newRepoNode.SaveFileNodeDatas)
        {
            newRepoNodeVM.SaveFileNodeDatas.Add((SaveFileNodeData)data.Clone());
        }

        // 创建子文件夹后，原先声明持有其父节点（或祖先节点）的FileNode，
        // 有可能因为没有同名的子节点而导致声明持有失效。
        // 所以需要向上查找，对所有被声明持有的祖先节点重新进行合法性检查。
        CheckAncestorsDeclarationStatus(repoNodeVM.RepoNode);

        Console.WriteLine($"创建子文件夹: {folderName}");
        System.Diagnostics.Debug.WriteLine($"创建子文件夹: {folderName}");
    }

    /// <summary>
    /// 尝试为一个节点建立 SaveFileNodeDatas。
    /// 逻辑：查看其父节点有哪些 SaveFileNodeDatas，然后在对应的 FileNode 中找是否有同名的子节点。
    /// </summary>
    private void TryEstablishSaveFileNodeDatasForNode(RepoNode node)
    {
        var parent = node.Parent as RepoNode;
        if (parent == null || !parent.SaveFileNodeDatas.Any())
            return;

        foreach (var saveData in parent.SaveFileNodeDatas)
        {
            var bundle = FileDataVmBundles.FirstOrDefault(b => b.FileData.DiskLabel == saveData.DiskLabel);
            if (bundle != null)
            {
                var parentFileNodeVm = FindFileNodeVmByPath(bundle.FileNodeVm, saveData.FileNodePath, out _);
                if (parentFileNodeVm != null)
                {
                    var matchingChildFileNodeVm = parentFileNodeVm.Children.FirstOrDefault(c => c.Name == node.Name);
                    
                    // 检查是否已经存在（避免重复添加）
                    bool alreadyExists = node.SaveFileNodeDatas.Any(d => d.DiskLabel == bundle.FileData.DiskLabel && d.FileNodePath == matchingChildFileNodeVm?.FileNode.GetPath());

                    if (matchingChildFileNodeVm != null && !alreadyExists)
                    {
                        var childFileNodePath = matchingChildFileNodeVm.FileNode.GetPath();
                        var newSaveData = new SaveFileNodeData
                        {
                            DiskLabel = bundle.FileData.DiskLabel,
                            FileNodePath = childFileNodePath
                        };
                        node.SaveFileNodeDatas.Add(newSaveData);
                        
                        var newDeclareData = new DeclareRepoNodeData
                        {
                            RepoNodePath = node.GetPath()
                        };
                        matchingChildFileNodeVm.FileNode.DeclareRepoNodeDatas.Add(newDeclareData);
                        matchingChildFileNodeVm.DeclareRepoNodeDatas.Add((DeclareRepoNodeData)newDeclareData.Clone());
                        
                        // 同时同步给当前的 VM (如果是通过重命名或其他操作调用此方法的)
                        var repoNodeVm = FindRepoNodeVmByPath(RepoNodeVm, node.GetPath(), out _);
                        if (repoNodeVm != null && !repoNodeVm.SaveFileNodeDatas.Any(d => d.DiskLabel == bundle.FileData.DiskLabel && d.FileNodePath == matchingChildFileNodeVm.FileNode.GetPath()))
                        {
                            repoNodeVm.SaveFileNodeDatas.Add((SaveFileNodeData)newSaveData.Clone());
                        }
                    }
                }
            }
        }
    }

    private void CheckAncestorsDeclarationStatus(RepoNode node)
    {
        var current = node;
        while (current != null)
        {
            // 只需要检查当前节点自身的声明关系，不需要向下递归收集所有受影响节点
            var specificAffectedNodes = GetAffectedFileNodes(current, includeDescendants: false);
            
            UpdateAffectedFileNodesDeclaration(specificAffectedNodes);
            
            current = current.Parent as RepoNode;
        }
    }
    
    /// <summary>
    /// 重命名 RepoNode 节点（仅对 Repo 节点生效）
    /// </summary>
    private async Task RenameRepoNodeAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        // 名称中包含 '/' 会破坏路径系统
        var oldPath = repoNodeVM.RepoNode.GetPath();

        var dialog = new RenameRepoNodeDialog(repoNodeVM.Name)
        {
            Title = "重命名",
            Width = 420,
            Height = 160,
        };

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;

        var result = await dialog.ShowDialog<string?>(owner);
        if (string.IsNullOrWhiteSpace(result))
            return; // 取消

        var newName = result.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName.Contains('/'))
        {
            Debug.WriteLine($"RenameRepoNodeAsync: invalid name '{newName}'");
            return;
        }

        if (string.Equals(newName, repoNodeVM.Name, StringComparison.Ordinal))
            return;

        // 避免同级重名（会导致通过 path 查找不稳定）
        var parent = repoNodeVM.RepoNode.Parent as RepoNode;
        if (parent != null)
        {
            var hasConflict = parent.Children
                .OfType<RepoNode>()
                .Any(x => !ReferenceEquals(x, repoNodeVM.RepoNode)
                          && string.Equals(x.Name, newName, StringComparison.Ordinal));
            if (hasConflict)
            {
                Debug.WriteLine($"RenameRepoNodeAsync: sibling name conflict '{newName}'");
                return;
            }
        }

        // 真正改名：Model + VM
        repoNodeVM.RepoNode.Name = newName;
        repoNodeVM.Name = newName;

        var newPath = repoNodeVM.RepoNode.GetPath();
        UpdateRepoNodePathReferences(oldPath, newPath);

        // 获取需要检查更新的相关的 FileNode 信息
        // (重命名可能导致原本符合声明的节点现在不符合了，需要被取消持有)
        var affectedFileNodes = GetAffectedFileNodes(repoNodeVM.RepoNode, includeDescendants: false);
        UpdateAffectedFileNodesDeclaration(affectedFileNodes);

        // 如果重命名后，满足了父节点对应 FileNode 中的子文件夹名称，
        // 则应该重新建立这个新建/重命名的文件夹的 SaveData 联系。
        TryEstablishSaveFileNodeDatasForNode(repoNodeVM.RepoNode);
        
        // 同步可能的 SaveFileNodeDatas 更新到 VM
        repoNodeVM.SaveFileNodeDatas.Clear();
        foreach (var data in repoNodeVM.RepoNode.SaveFileNodeDatas)
        {
            repoNodeVM.SaveFileNodeDatas.Add((SaveFileNodeData)data.Clone());
        }

        // 重命名后，可能会破坏声明持有状态，或者使之前不符合的重新符合。
        // 根据要求，修改名字后如果不满足则取消持有，因此要检查祖先的声明状态
        CheckAncestorsDeclarationStatus(repoNodeVM.RepoNode);

        // 也可以顺便尝试恢复那些因为曾经缺失该文件夹而丢失声明持有的祖先节点，
        // 这里提供一个恢复逻辑的调用入口，如果有缓存或特定需求可以加上，
        // 但根据目前的规则，主要处理因名称不对而掉落的问题。

        // 更新路径输入框（会触发重新定位/展开）
        RepoNodePathString = newPath;

        Debug.WriteLine($"RenameRepoNodeAsync: {oldPath} -> {newPath}");
    }

    /// <summary>
    /// 删除 RepoNode 节点（仅对 Repo 节点生效）
    /// </summary>
    private async Task DeleteRepoNodeAsync(object nodeVM)
    {
        if (nodeVM is not RepoNodeVM repoNodeVM)
            return;

        if (repoNodeVM.RepoNode == RepoNodeRoot)
        {
            // 根节点不允许删除
            Debug.WriteLine("DeleteRepoNodeAsync: Cannot delete root node.");
            return;
        }

        var dialog = new DeleteConfirmDialog(repoNodeVM.Name)
        {
            Title = "确认删除",
            Width = 400,
            Height = 150,
        };

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;

        var result = await dialog.ShowDialog<bool>(owner);
        if (!result)
            return; // 取消或关闭弹窗

        var parent = repoNodeVM.RepoNode.Parent as RepoNode;
        if (parent == null)
            return;

        // 获取需要检查更新的相关的 FileNode 信息
        // 任何存储了该节点或其子节点的 FileNode 都可能受到影响
        var affectedFileNodes = GetAffectedFileNodes(repoNodeVM.RepoNode);

        // 获取当前节点的祖先节点（它们可能会因为删除操作而改变了包含结构，需要重新检查声明状态）
        // 因为是从该节点向下寻找声明关系的节点，如果删除了该节点，那么祖先节点包含的范围变小了，可能不再包含所有的FileNode，导致原本成功的声明失败。
        var ancestors = new List<RepoNode>();
        var currAncestor = parent;
        while (currAncestor != null)
        {
            ancestors.Add(currAncestor);
            currAncestor = currAncestor.Parent as RepoNode;
        }

        // Model 中删除
        parent.Children.Remove(repoNodeVM.RepoNode);
        
        // VM 中删除
        var parentVm = FindRepoNodeVmByPath(RepoNodeVm, parent.GetPath(), out _);
        if (parentVm != null)
        {
            parentVm.Children.Remove(repoNodeVM);
        }

        // 更新受影响的 FileNodes 的声明状态
        UpdateAffectedFileNodesDeclaration(affectedFileNodes);

        // 检查祖先节点的声明状态
        foreach (var ancestor in ancestors)
        {
            CheckAncestorsDeclarationStatus(ancestor);
        }

        Debug.WriteLine($"DeleteRepoNodeAsync: deleted {repoNodeVM.RepoNode.GetPath()}");
    }

    private List<(FileNode FileNode, RepoNode OriginalRepoNode)> GetAffectedFileNodes(RepoNode targetNode, bool includeDescendants = true)
    {
        var result = new List<(FileNode, RepoNode)>();
        var targetPathExact = targetNode.GetPath();
        var targetPathPrefix = targetPathExact + "/";

        void CollectFromTree(FileNode fileNode)
        {
            if (fileNode.DeclareRepoNodeDatas != null)
            {
                foreach (var declareData in fileNode.DeclareRepoNodeDatas)
                {
                    bool isMatch = declareData.RepoNodePath == targetPathExact ||
                                   (includeDescendants && declareData.RepoNodePath.StartsWith(targetPathPrefix, StringComparison.Ordinal));
                                   
                    if (isMatch)
                    {
                        var originalRepoNode = TreeNodeUtils.GetNodeByPathFromRoot(RepoNodeRoot, declareData.RepoNodePath) as RepoNode;
                        if (originalRepoNode != null)
                        {
                            result.Add((fileNode, originalRepoNode));
                        }
                    }
                }
            }
            
            foreach (var child in fileNode.Children.OfType<FileNode>())
            {
                CollectFromTree(child);
            }
        }

        foreach (var bundle in FileDataVmBundles)
        {
            if (bundle.FileData?.FileNodeRoot != null)
            {
                CollectFromTree(bundle.FileData.FileNodeRoot);
            }
        }
        
        return result;
    }

    private void UpdateAffectedFileNodesDeclaration(List<(FileNode FileNode, RepoNode OriginalRepoNode)> affectedNodes)
    {
        foreach (var (fileNode, repoNode) in affectedNodes)
        {
            // 找到声明这个 repo 节点的 declare 数据
            var repoPath = repoNode.GetPath();
            var declareData = fileNode.DeclareRepoNodeDatas.FirstOrDefault(d => d.RepoNodePath == repoPath);
            
            if (declareData != null)
            {
                // 由于节点已经被删除（或者其父节点被删除，从而它也不在树里了）
                // 我们尝试从 Root 重新找这个节点。如果找不到，说明被删了，声明关系失效。
                var currentRepoNodeInTree = TreeNodeUtils.GetNodeByPathFromRoot(RepoNodeRoot, repoPath) as RepoNode;
                
                // 如果在树上找不到了，或者找到了但检查状态不再符合条件
                if (currentRepoNodeInTree == null || !TreeNodeUtils.CheckDeclarationStatus(currentRepoNodeInTree, fileNode))
                {
                    // 移除声明持有
                    fileNode.DeclareRepoNodeDatas.Remove(declareData);
                    
                    // 如果这个 RepoNode 还活着，但是因为子树变化导致声明失效，
                    // 那么这个 RepoNode 的 SaveFileNodeDatas 里面的相关信息也应该删掉。
                    if (currentRepoNodeInTree != null)
                    {
                        var saveData = currentRepoNodeInTree.SaveFileNodeDatas.FirstOrDefault(d => d.FileNodePath == fileNode.GetPath());
                        if (saveData != null)
                        {
                            currentRepoNodeInTree.SaveFileNodeDatas.Remove(saveData);
                            // 同步 VM
                            var repoNodeVm = FindRepoNodeVmByPath(RepoNodeVm, currentRepoNodeInTree.GetPath(), out _);
                            if (repoNodeVm != null)
                            {
                                var vmSaveData = repoNodeVm.SaveFileNodeDatas.FirstOrDefault(d => d.FileNodePath == fileNode.GetPath());
                                if (vmSaveData != null)
                                {
                                    repoNodeVm.SaveFileNodeDatas.Remove(vmSaveData);
                                }
                            }
                        }
                    }
                    
                    // 同步更新对应的 VM
                    foreach (var bundle in FileDataVmBundles)
                    {
                        var vm = FindFileNodeVmByPath(bundle.FileNodeVm, fileNode.GetPath(), out _);
                        if (vm != null)
                        {
                            var vmDeclareData = vm.DeclareRepoNodeDatas.FirstOrDefault(d => d.RepoNodePath == repoPath);
                            if (vmDeclareData != null)
                            {
                                vm.DeclareRepoNodeDatas.Remove(vmDeclareData);
                            }
                        }
                    }

                    Debug.WriteLine($"Removed declaration: FileNode '{fileNode.GetPath()}' no longer holds RepoNode '{repoPath}'");
                }
            }
        }
    }

    private void UpdateRepoNodePathReferences(string oldPath, string newPath)
    {
        if (string.IsNullOrWhiteSpace(oldPath))
            return;

        foreach (var bundle in FileDataVmBundles)
        {
            if (bundle?.FileData?.FileNodeRoot != null)
            {
                UpdateDeclareRepoNodePaths(bundle.FileData.FileNodeRoot, oldPath, newPath);
            }

            if (bundle?.FileNodeVm != null)
            {
                UpdateDeclareRepoNodePaths(bundle.FileNodeVm, oldPath, newPath);
            }
        }
    }

    private static void UpdateDeclareRepoNodePaths(FileNode node, string oldPath, string newPath)
    {
        if (node.DeclareRepoNodeDatas != null)
        {
            foreach (var data in node.DeclareRepoNodeDatas)
            {
                if (data?.RepoNodePath == null)
                    continue;

                data.RepoNodePath = ReplacePathPrefix(data.RepoNodePath, oldPath, newPath);
            }
        }

        foreach (var child in node.Children.OfType<FileNode>())
        {
            UpdateDeclareRepoNodePaths(child, oldPath, newPath);
        }
    }

    private static void UpdateDeclareRepoNodePaths(FileNodeVM node, string oldPath, string newPath)
    {
        if (node.DeclareRepoNodeDatas != null)
        {
            foreach (var data in node.DeclareRepoNodeDatas)
            {
                if (data?.RepoNodePath == null)
                    continue;

                data.RepoNodePath = ReplacePathPrefix(data.RepoNodePath, oldPath, newPath);
            }
        }

        foreach (var child in node.Children)
        {
            UpdateDeclareRepoNodePaths(child, oldPath, newPath);
        }
    }

    private static string ReplacePathPrefix(string path, string oldPrefix, string newPrefix)
    {
        // 精确匹配：oldPrefix 或 oldPrefix/xxx
        if (string.Equals(path, oldPrefix, StringComparison.Ordinal))
            return newPrefix;

        var boundary = oldPrefix.EndsWith("/", StringComparison.Ordinal) ? oldPrefix : (oldPrefix + "/");
        if (path.StartsWith(boundary, StringComparison.Ordinal))
        {
            return newPrefix + path.Substring(oldPrefix.Length);
        }

        return path;
    }

    /// <summary>
    /// 输出节点的路径到日志
    /// </summary>
    private void LogNodePath(object nodeVM)
    {
        if (nodeVM is RepoNodeVM repoNodeVM)
        {
            var path = repoNodeVM.RepoNode.GetPath();
            Console.WriteLine($"仓库节点路径: {path}");
            System.Diagnostics.Debug.WriteLine($"仓库节点路径: {path}");
            
            var saveDatas = repoNodeVM.RepoNode.SaveFileNodeDatas;
            Console.WriteLine($"  SaveFileNodeDatas 数量: {saveDatas.Count}");
            System.Diagnostics.Debug.WriteLine($"  SaveFileNodeDatas 数量: {saveDatas.Count}");
            
            foreach (var data in saveDatas)
            {
                Console.WriteLine($"    - DiskLabel: {data.DiskLabel}, FileNodePath: {data.FileNodePath}");
                System.Diagnostics.Debug.WriteLine($"    - DiskLabel: {data.DiskLabel}, FileNodePath: {data.FileNodePath}");
            }
        }
        else if (nodeVM is FileNodeVM fileNodeVM)
        {
            var path = fileNodeVM.FileNode.GetPath();
            Console.WriteLine($"文件节点路径: {path}");
            System.Diagnostics.Debug.WriteLine($"文件节点路径: {path}");

            var declareDatas = fileNodeVM.FileNode.DeclareRepoNodeDatas;
            Console.WriteLine($"  DeclareRepoNodeDatas 数量: {declareDatas.Count}");
            System.Diagnostics.Debug.WriteLine($"  DeclareRepoNodeDatas 数量: {declareDatas.Count}");
            
            foreach (var data in declareDatas)
            {
                Console.WriteLine($"    - RepoNodePath: {data.RepoNodePath}");
                System.Diagnostics.Debug.WriteLine($"    - RepoNodePath: {data.RepoNodePath}");
            }
        }
        else
        {
            Console.WriteLine("未知节点类型");
            System.Diagnostics.Debug.WriteLine("未知节点类型");
        }
    }

    /// <summary>
    /// 打开弹窗，让用户选择文件夹和FileTree名
    /// </summary>
    public async void OpenCreateNewFileTreeDialog()
    {
        Debug.WriteLine("OpenCreateNewFileTreeDialog");
        var dialog = new FolderSelectDialog
        {
            Title = "选择文件夹并填写标签",
            Width = 450,
            Height = 150,
            // DataContext = new FolderSelectDialogViewModel(),
        };

        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow;
        // ShowDialog 的返回值就是 ViewModel 中传出的 Tuple<string?, string?>
        var result = await dialog.ShowDialog<(string? path, string? tag)?>(window);

        if (result is { path: not null, tag: not null })
        {
            Console.WriteLine($"选中的文件夹: {result?.path}");
            Console.WriteLine($"填写的标签: {result?.tag}");

            var bundle = FileDataVMBundle.CreateByPath(
                result?.tag ?? string.Empty,
                result?.path ?? string.Empty);
            FileDataVmBundles.Add(bundle);
            DiskLabels.Add(bundle.FileData.DiskLabel);
        }
    }

    #endregion

    #region Utils

    private RepoNodeVM? FindRepoNodeVmByPath(
        RepoNodeVM root,
        string path,
        out IndexPath? indexPath)
    {
        var ret = TreeNodeVMBase<RepoNodeVM>.FindTreeNodeVmByPath(
            root,
            path,
            out indexPath);
        return ret as RepoNodeVM;
    }

    private FileNodeVM? FindFileNodeVmByPath(
        FileNodeVM root,
        string path,
        out IndexPath? indexPath)
    {
        var ret = TreeNodeVMBase<FileNodeVM>.FindTreeNodeVmByPath(
            root,
            path,
            out indexPath);
        return ret as FileNodeVM;
    }

    /// <summary>
    /// 检查当前选中的repo node和file node是否同步，防止循环跳转
    /// </summary>
    private bool CheckRepoNodeAndFileNodeIsSync()
    {
        var repoNode = RepoNodeSource.RowSelection
            ?.SelectedItem?.RepoNode ?? null;
        var fileNode = CurrFileNodeSource.RowSelection
            ?.SelectedItem?.FileNode ?? null;
        if (repoNode == null && fileNode == null)
            return true;
        if (repoNode == null || fileNode == null)
            return false;
        if (fileNode.DeclareRepoNodeDatas.Count == 0)
            return false;
        foreach (var declareRepoNodeData in fileNode.DeclareRepoNodeDatas)
        {
            var foundRepoNode = TreeNodeUtils.GetNodeByPathFromRoot(
                RepoNodeRoot,
                declareRepoNodeData.RepoNodePath);
            if (repoNode == foundRepoNode)
                return true;
        }

        return false;
    }

    #endregion Utils

    #region 测试功能

    public void ShowRepoNode()
    {
        var rowSelection = RepoNodeSource.RowSelection;
        var select = rowSelection?.SelectedItem ?? null;
        Console.WriteLine(select?.Name ?? "");
    }

    #endregion
}