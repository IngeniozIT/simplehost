namespace SimpleHost.Application.Tray;

/// <summary>
/// Identifies which quick action a <see cref="TrayMenuItem"/> triggers when chosen from the system
/// tray, so the thin presentation shell can map each menu entry onto the effect it must carry out
/// without duplicating the menu's structure or wording.
/// </summary>
public enum TrayMenuItemKind
{
    /// <summary>Brings the main SimpleHost window to the foreground, opening it if it is closed.</summary>
    OpenMainWindow,

    /// <summary>Quits SimpleHost, removing the tray icon and ending the process.</summary>
    Exit,
}
