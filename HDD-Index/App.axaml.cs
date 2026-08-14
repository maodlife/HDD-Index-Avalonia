using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using HDD_Index.Adapters;
using HDD_Index.Application.Declarations;
using HDD_Index.Application.FileTrees;
using HDD_Index.Application.Persistence;
using HDD_Index.Application.Repositories;
using HDD_Index.Application.Startup;
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
            var atomicFileWriter = new AtomicFileWriter();
            var appConfigService = new AppConfigService(atomicFileWriter);
            var treeDataStore = new TreeDataStore(atomicFileWriter);
            var sessionStore = new JsonApplicationSessionStore(
                appConfigService,
                treeDataStore);
            var startupService = new ApplicationStartupService(
                appConfigService.GetDefaultConfigPath(),
                appConfigService,
                treeDataStore,
                sessionStore);
            var startupResult = startupService.LoadDefault();
            if (startupResult.State == ApplicationStartupState.Ready)
            {
                desktop.MainWindow = CreateMainWindow(
                    startupResult.Session!,
                    sessionStore,
                    startupResult.Warnings);
            }
            else
            {
                var startupWindow = new StartupWindow();
                var startupInteraction = new AvaloniaStartupInteraction(startupWindow);
                startupWindow.DataContext = new StartupViewModel(
                    startupResult,
                    startupService,
                    startupInteraction,
                    result => CompleteStartup(
                        desktop,
                        startupWindow,
                        sessionStore,
                        result));
                desktop.MainWindow = startupWindow;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void CompleteStartup(
        IClassicDesktopStyleApplicationLifetime desktop,
        StartupWindow startupWindow,
        JsonApplicationSessionStore sessionStore,
        ApplicationStartupResult? result)
    {
        if (result?.Session == null)
        {
            desktop.Shutdown();
            return;
        }

        var mainWindow = CreateMainWindow(
            result.Session,
            sessionStore,
            result.Warnings);
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        startupWindow.Close();
    }

    private static MainWindow CreateMainWindow(
        ApplicationSession session,
        JsonApplicationSessionStore sessionStore,
        IReadOnlyList<SessionLoadIssue> startupWarnings)
    {
        var sessionManager = new ApplicationSessionManager(
            session,
            sessionStore);
        var fileTreeScanner = new FileTreeScanner();

        var treeProjection = new TreeProjection();
        var repoBrowser = new RepoBrowserViewModel(
            session.RepoNodeRoot,
            treeProjection);
        var fileBrowser = new FileBrowserViewModel(
            session.FileDatas,
            treeProjection);
        var repositoryEditor = new RepositoryEditorViewModel();
        var declarationSyncService = new DeclarationSyncService(
            session.RepoNodeRoot,
            session.FileDatas);
        var declarationUseCases = new DeclarationUseCases(declarationSyncService);
        var repoTreeEditor = new RepoTreeEditor(declarationSyncService);
        var repositoryUseCases = new RepositoryUseCases(
            repoTreeEditor,
            session.FileDatas);
        var fileTreeEditor = new FileTreeEditor(declarationSyncService);
        var fileTreeUseCases = new FileTreeUseCases(
            session,
            fileTreeEditor,
            fileTreeScanner,
            new FileTreePathService());

        var mainWindow = new MainWindow();
        var userInteraction = new AvaloniaUserInteraction(mainWindow);
        var repositoryInteraction = new AvaloniaRepositoryInteraction(mainWindow);
        var fileTreeInteraction = new AvaloniaFileTreeInteraction(mainWindow);
        var scanProgressRunner = new AvaloniaFileTreeScanProgressRunner(mainWindow);
        var pathOpener = new WindowsExplorerPathOpener();

        mainWindow.DataContext = new MainWindowViewModel(
            sessionManager,
            declarationUseCases,
            repositoryUseCases,
            fileTreeUseCases,
            treeProjection,
            repoBrowser,
            fileBrowser,
            repositoryEditor,
            userInteraction,
            repositoryInteraction,
            fileTreeInteraction,
            scanProgressRunner,
            pathOpener,
            startupWarnings);
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
