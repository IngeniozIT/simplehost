namespace SimpleHost.Application.Tray;

/// <summary>
/// The whole model the system tray icon is built from: the tooltip that names the running app on
/// hover and the ordered quick actions its menu offers. It describes what the tray presents while
/// SimpleHost runs, keeping the icon reachable without the main window, and is produced by
/// <see cref="TrayMenuFactory"/> so the presentation shell only has to render it.
/// </summary>
/// <param name="Tooltip">The text shown when hovering the tray icon, naming the running app.</param>
/// <param name="Items">The quick actions the tray menu offers, in the order they are shown.</param>
public sealed record TrayMenu(string Tooltip, IReadOnlyList<TrayMenuItem> Items);
