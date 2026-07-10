using System.Drawing;
using System.Windows.Forms;
using SimpleHost.Application.Tray;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// The thin presentation shell around a Windows notification-area (system tray) icon. It renders the
/// <see cref="TrayMenu"/> the <see cref="TrayMenuController"/> builds from the running app's live
/// state as a tray icon that stays visible for the whole time the app runs, and routes each chosen
/// entry to its effect: opening the main window or exiting are carried out here, while a category
/// toggle or profile switch is handed to the controller, which applies it directly to the running
/// app — after which the menu is rebuilt so the tray stays in sync with the new state.
/// Double-clicking the icon opens the main window, matching Windows' usual tray behaviour. Disposing
/// removes the icon from the tray.
/// </summary>
public sealed class SimpleHostTrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayMenuController _controller;
    private readonly Action _openMainWindow;
    private readonly Action _exit;

    public SimpleHostTrayIcon(TrayMenuController controller, Action openMainWindow, Action exit)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(openMainWindow);
        ArgumentNullException.ThrowIfNull(exit);

        _controller = controller;
        _openMainWindow = openMainWindow;
        _exit = exit;

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
        };

        RefreshMenu();

        _notifyIcon.DoubleClick += (_, _) => _openMainWindow();
    }

    private void RefreshMenu()
    {
        var menu = _controller.BuildMenu();
        _notifyIcon.Text = menu.Tooltip;

        var contextMenu = new ContextMenuStrip();
        foreach (var item in menu.Items)
        {
            contextMenu.Items.Add(item.Label, image: null, (_, _) => Choose(item));
        }

        var previous = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = contextMenu;
        previous?.Dispose();
    }

    private void Choose(TrayMenuItem item)
    {
        switch (item.Kind)
        {
            case TrayMenuItemKind.OpenMainWindow:
                _openMainWindow();
                break;
            case TrayMenuItemKind.Exit:
                _exit();
                break;
            default:
                // A category toggle or profile switch: apply it directly to the running app, then
                // rebuild the menu so the tray reflects the new state (e.g. a just-disabled category
                // now offers to enable it).
                _controller.Apply(item);
                RefreshMenu();
                break;
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
