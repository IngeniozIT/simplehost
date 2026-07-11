using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
    // The system tray shows a small (~16px) icon. It is drawn from the same source image as every
    // other app icon: the app-icon.png WPF resource, loaded and downscaled at runtime.
    private static readonly Uri AppIconUri = new("pack://application:,,,/Assets/app-icon.png");

    private readonly NotifyIcon _notifyIcon;
    private readonly TrayMenuController _controller;
    private readonly Action _openMainWindow;
    private readonly Action _exit;

    // The unmanaged HICON backing the tray icon. Icon.FromHandle does not own its handle, so we keep
    // it to destroy explicitly on Dispose (IntPtr.Zero when the fallback system icon is used instead).
    private IntPtr _trayIconHandle;

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
            Icon = LoadTrayIcon() ?? SystemIcons.Application,
            Visible = true,
        };

        RefreshMenu();

        _notifyIcon.DoubleClick += (_, _) => _openMainWindow();
    }

    // Loads the app-icon.png resource and turns it into a crisp 16x16 tray icon. Returns null (so the
    // caller falls back to the default system icon) if the resource is missing or cannot be decoded,
    // so a bad icon can never stop the app from launching. System.Drawing is used only here, at
    // runtime on Windows — the cross-platform Linux build never touches it (the .ico is produced from
    // the same PNG by an MSBuild step; see SimpleHost.Presentation.csproj).
    private Icon? LoadTrayIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(AppIconUri);
            if (resource is null)
            {
                return null;
            }

            using var stream = resource.Stream;
            using var source = new Bitmap(stream);
            using var scaled = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, 16, 16));
            }

            _trayIconHandle = scaled.GetHicon();
            return Icon.FromHandle(_trayIconHandle);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

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

        // Release the unmanaged HICON created by GetHicon (Icon.FromHandle did not take ownership).
        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }
    }
}
