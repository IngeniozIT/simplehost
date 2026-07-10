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

    /// <summary>
    /// Enables every entry of the targeted category in place, so the whole group resolves again,
    /// applied straight from the tray without opening the main window.
    /// </summary>
    EnableCategory,

    /// <summary>
    /// Disables every entry of the targeted category in place, so the whole group stays in the
    /// <c>hosts</c> file but no longer resolves, applied straight from the tray without opening the
    /// main window.
    /// </summary>
    DisableCategory,

    /// <summary>
    /// Activates the targeted saved profile, switching the whole <c>hosts</c> file to its preset,
    /// applied straight from the tray without opening the main window.
    /// </summary>
    ActivateProfile,

    /// <summary>Quits SimpleHost, removing the tray icon and ending the process.</summary>
    Exit,
}
