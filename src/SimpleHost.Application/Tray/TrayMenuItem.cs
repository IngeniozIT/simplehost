namespace SimpleHost.Application.Tray;

/// <summary>
/// A single quick action offered by the system tray icon's menu: the user-visible wording to show
/// for it, the <see cref="TrayMenuItemKind"/> the presentation shell runs when it is chosen, and the
/// category or profile the action applies to. The menu's wording and structure live here, in a
/// testable layer, so the WPF tray icon stays a thin shell that only wires each item to its effect.
/// </summary>
/// <param name="Label">The user-visible text for the menu entry, as shown in the tray menu.</param>
/// <param name="Kind">The quick action carried out when the entry is chosen.</param>
/// <param name="Target">
/// The subject the action applies to: the name of the category to enable or disable (with
/// <c>null</c> standing for the "No category" group), or the name of the profile to activate.
/// <c>null</c> for actions that need no subject, such as opening the main window or exiting.
/// </param>
public sealed record TrayMenuItem(string Label, TrayMenuItemKind Kind, string? Target = null);
