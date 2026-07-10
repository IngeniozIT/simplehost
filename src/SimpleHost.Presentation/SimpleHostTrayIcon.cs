using System.Drawing;
using System.Windows.Forms;
using SimpleHost.Application.Tray;

namespace SimpleHost.Presentation;

/// <summary>
/// The thin presentation shell around a Windows notification-area (system tray) icon. It renders a
/// <see cref="TrayMenu"/> — the tooltip and quick actions decided in the testable application layer —
/// as a live tray icon that stays visible for the whole time the app runs, and wires each menu entry
/// onto the effect the shell carries out (opening the main window or exiting). Double-clicking the
/// icon opens the main window, matching Windows' usual tray behaviour. Disposing removes the icon
/// from the tray.
/// </summary>
public sealed class SimpleHostTrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public SimpleHostTrayIcon(TrayMenu menu, Action openMainWindow, Action exit)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(openMainWindow);
        ArgumentNullException.ThrowIfNull(exit);

        _notifyIcon = new NotifyIcon
        {
            Text = menu.Tooltip,
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildContextMenu(menu, openMainWindow, exit),
        };

        _notifyIcon.DoubleClick += (_, _) => openMainWindow();
    }

    private static ContextMenuStrip BuildContextMenu(TrayMenu menu, Action openMainWindow, Action exit)
    {
        var contextMenu = new ContextMenuStrip();

        foreach (var item in menu.Items)
        {
            var effect = item.Kind switch
            {
                TrayMenuItemKind.OpenMainWindow => openMainWindow,
                TrayMenuItemKind.Exit => exit,
                _ => throw new NotSupportedException($"Unsupported tray action: {item.Kind}."),
            };

            contextMenu.Items.Add(item.Label, image: null, (_, _) => effect());
        }

        return contextMenu;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
