using System.Windows;
using SimpleHost.Application.Appearance;
using SimpleHost.Application.Tray;
using SimpleHost.Infrastructure.Appearance;
using SimpleHost.Presentation.Composition;
using SimpleHost.Presentation.DeepLinks;
using SimpleHost.Presentation.ViewModels;
using SimpleHost.Presentation.ViewModels.DeepLinks;

namespace SimpleHost.Presentation;

/// <summary>
/// The SimpleHost WPF application. It starts tray-first: rather than opening a window on launch, it
/// installs a system tray icon that stays available for the whole time the process runs and gives
/// access to the app's quick actions. The main window is only shown on demand from the tray, and
/// closing it leaves the app running in the tray — the app quits only through the tray's Exit action
/// (hence <c>ShutdownMode=OnExplicitShutdown</c>).
///
/// On startup it also begins following the Windows light/dark theme: it reads the current system
/// setting and applies the matching appearance, then switches the whole app whenever the user changes
/// the system setting, so SimpleHost always looks at home next to the rest of Windows.
/// </summary>
public partial class App : System.Windows.Application
{
    private SimpleHostTrayIcon? _trayIcon;
    private WindowsSystemThemeProvider? _themeProvider;
    private SystemThemeWatcher? _themeWatcher;
    private CompositionRoot? _compositionRoot;
    private MainViewModel? _mainViewModel;
    private SingleInstanceDeepLinkService? _deepLinkService;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Keep SimpleHost to a single running instance. When a simplehost:// link opens a second copy,
        // hand the link to the instance already running and exit, so subsequent links are handled by
        // the one running app rather than starting another copy.
        _deepLinkService = new SingleInstanceDeepLinkService();
        if (!_deepLinkService.IsPrimary)
        {
            if (DeepLinkCommandLine.TryExtractLink(e.Args, out var forwardedLink))
            {
                SingleInstanceDeepLinkService.ForwardLink(forwardedLink);
            }

            _deepLinkService.Dispose();
            _deepLinkService = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        StartFollowingSystemTheme();

        // Resolve the hosts file from the command line (or environment), wire the whole object graph
        // behind it, and load it so the main window opens showing real data. Pointing the app at a
        // local sample file with --hosts-file needs no elevation; only the protected system file does.
        _compositionRoot = new CompositionRoot();
        _mainViewModel = _compositionRoot.BuildMainViewModel(e.Args);
        _mainViewModel.Load();

        // Share the one view-model between the tray and the main window, so a quick toggle chosen from
        // the tray is applied straight to the running app and is reflected in the window when it is open.
        var trayController = new TrayMenuController(_mainViewModel, new TrayMenuFactory());
        _trayIcon = new SimpleHostTrayIcon(trayController, ShowMainWindow, Shutdown);

        // Route links forwarded by future second instances into the running app, and act on a link
        // this instance may itself have been launched with.
        _deepLinkService.StartListening(OnDeepLinkReceived);
        if (DeepLinkCommandLine.TryExtractLink(e.Args, out var launchLink))
        {
            OnDeepLinkReceived(launchLink);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _themeWatcher?.Dispose();
        _themeProvider?.Dispose();
        _compositionRoot?.Dispose();
        _deepLinkService?.Dispose();
        base.OnExit(e);
    }

    private void OnDeepLinkReceived(string link)
    {
        // Forwarded links arrive on a background thread; marshal to the UI thread, bring the window
        // forward so the staged change is visible, then route the link through the view-model, which
        // prompts for confirmation on the destructive update, delete and disable actions.
        Dispatcher.Invoke(() =>
        {
            ShowMainWindow();
            _mainViewModel!.HandleDeepLink(link);
        });
    }

    private void StartFollowingSystemTheme()
    {
        var applier = new ThemeApplier(this);

        _themeProvider = new WindowsSystemThemeProvider();
        _themeWatcher = new SystemThemeWatcher(_themeProvider);

        // System theme notifications arrive on a background thread; apply on the UI thread.
        _themeWatcher.ThemeChanged += theme => Dispatcher.Invoke(() => applier.Apply(theme));

        applier.Apply(_themeWatcher.CurrentTheme);
    }

    private void ShowMainWindow()
    {
        MainWindow ??= new MainWindow(_mainViewModel!);
        MainWindow.Show();

        if (MainWindow.WindowState == WindowState.Minimized)
        {
            MainWindow.WindowState = WindowState.Normal;
        }

        MainWindow.Activate();
    }
}
