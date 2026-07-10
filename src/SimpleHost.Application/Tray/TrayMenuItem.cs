namespace SimpleHost.Application.Tray;

/// <summary>
/// A single quick action offered by the system tray icon's menu: the user-visible wording to show
/// for it and the <see cref="TrayMenuItemKind"/> the presentation shell runs when it is chosen. The
/// menu's wording and structure live here, in a testable layer, so the WPF tray icon stays a thin
/// shell that only wires each item to its effect.
/// </summary>
/// <param name="Label">The user-visible text for the menu entry, as shown in the tray menu.</param>
/// <param name="Kind">The quick action carried out when the entry is chosen.</param>
public sealed record TrayMenuItem(string Label, TrayMenuItemKind Kind);
