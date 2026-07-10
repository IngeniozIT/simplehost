using System.Windows;
using SimpleHost.Application.Tray;

namespace SimpleHost.Presentation;

/// <summary>
/// The SimpleHost WPF application. It starts tray-first: rather than opening a window on launch, it
/// installs a system tray icon that stays available for the whole time the process runs and gives
/// access to the app's quick actions. The main window is only shown on demand from the tray, and
/// closing it leaves the app running in the tray — the app quits only through the tray's Exit action
/// (hence <c>ShutdownMode=OnExplicitShutdown</c>).
/// </summary>
public partial class App : System.Windows.Application
{
    private SimpleHostTrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIcon = new SimpleHostTrayIcon(new TrayMenuFactory().Create(), ShowMainWindow, Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void ShowMainWindow()
    {
        MainWindow ??= new MainWindow();
        MainWindow.Show();

        if (MainWindow.WindowState == WindowState.Minimized)
        {
            MainWindow.WindowState = WindowState.Normal;
        }

        MainWindow.Activate();
    }
}
