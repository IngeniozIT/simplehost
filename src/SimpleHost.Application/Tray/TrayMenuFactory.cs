namespace SimpleHost.Application.Tray;

/// <summary>
/// Builds the <see cref="TrayMenu"/> that SimpleHost's system tray icon shows for the whole time the
/// app runs, so the icon stays available and offers its quick actions without the main window having
/// to be open. The baseline menu lets the user open the main window and exit the app; it is the seam
/// later features (such as per-category or per-profile toggles) extend.
/// </summary>
public sealed class TrayMenuFactory
{
    /// <summary>
    /// Creates the tray menu shown while SimpleHost runs: an icon tooltip naming the app and, as its
    /// quick actions, opening the main window followed by exiting the app.
    /// </summary>
    public TrayMenu Create() => new(
        "SimpleHost",
        new[]
        {
            new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
            new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
        });
}
