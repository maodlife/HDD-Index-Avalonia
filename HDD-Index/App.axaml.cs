using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using HDD_Index.Adapters;
using HDD_Index.Services;
using HDD_Index.ViewModels;
using HDD_Index.Views;

namespace HDD_Index;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindow CreateMainWindow()
    {
        var appConfigService = new AppConfigService();
        var treeDataStore = new TreeDataStore();
        var dirtyJsonFileTracker = new DirtyJsonFileTracker();
        var fileTreeScanner = new FileTreeScanner();

        var appConfig = appConfigService.LoadDefault();
        var repoNodeRoot = treeDataStore.LoadRepoRoot(appConfig);
        var fileDatas = treeDataStore.LoadFileDatas(appConfig);
        var treeProjection = new TreeProjection();
        var repoBrowser = new RepoBrowserViewModel(repoNodeRoot, treeProjection);
        var fileBrowser = new FileBrowserViewModel(fileDatas, treeProjection);
        var repositoryEditor = new RepositoryEditorViewModel();
        var declarationSyncService = new DeclarationSyncService(
            repoNodeRoot,
            fileDatas);
        var repoTreeEditor = new RepoTreeEditor(declarationSyncService);
        var fileTreeEditor = new FileTreeEditor(declarationSyncService);

        var mainWindow = new MainWindow();
        var userInteraction = new AvaloniaUserInteraction(mainWindow);
        var repositoryInteraction = new AvaloniaRepositoryInteraction(mainWindow);
        var fileTreeInteraction = new AvaloniaFileTreeInteraction(mainWindow);
        var scanProgressRunner = new AvaloniaFileTreeScanProgressRunner(mainWindow);
        var pathOpener = new WindowsExplorerPathOpener();

        mainWindow.DataContext = new MainWindowViewModel(
            appConfig,
            appConfigService,
            treeDataStore,
            dirtyJsonFileTracker,
            declarationSyncService,
            repoTreeEditor,
            fileTreeEditor,
            fileTreeScanner,
            treeProjection,
            repoBrowser,
            fileBrowser,
            repositoryEditor,
            userInteraction,
            repositoryInteraction,
            fileTreeInteraction,
            scanProgressRunner,
            pathOpener);
        return mainWindow;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
