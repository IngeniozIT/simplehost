using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tray;

/// <summary>
/// Builds the <see cref="TrayMenu"/> that SimpleHost's system tray icon shows for the whole time the
/// app runs, so the icon stays available and offers its quick actions without the main window having
/// to be open. On top of the standing "open the main window" and "exit" actions, the menu offers a
/// quick toggle per category — enabling or disabling the whole group in place — and a "switch to"
/// entry per saved profile, so the user changes what resolves straight from the tray without opening
/// the main window.
/// </summary>
public sealed class TrayMenuFactory
{
    private const string NoCategoryLabel = "No category";

    /// <summary>
    /// Creates the baseline tray menu shown when there is nothing to toggle: an icon tooltip naming
    /// the app and, as its quick actions, opening the main window followed by exiting the app.
    /// </summary>
    public TrayMenu Create()
        => Create(Array.Empty<CategoryGroup>(), Array.Empty<string>());

    /// <summary>
    /// Creates the tray menu shown while SimpleHost runs: a quick toggle for each non-empty category
    /// (in the given order), then a "switch to" entry for each saved profile (in the given order),
    /// then the standing "open the main window" and "exit" actions. A category whose entries are all
    /// active offers a "disable" toggle; a category with any disabled entry offers an "enable" toggle
    /// that brings the whole group back on. The "No category" group is labelled as such and targeted
    /// with <c>null</c>. Categories with no entries have nothing to toggle and are left out.
    /// </summary>
    /// <param name="categories">
    /// The categories to offer toggles for, in the order they should appear; the "No category" group
    /// carries a <c>null</c> name.
    /// </param>
    /// <param name="profileNames">The names of the saved profiles to offer, in the order to show them.</param>
    public TrayMenu Create(IReadOnlyList<CategoryGroup> categories, IReadOnlyList<string> profileNames)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(profileNames);

        var items = new List<TrayMenuItem>();

        foreach (var category in categories.Where(category => category.Entries.Count > 0))
        {
            items.Add(ToggleFor(category));
        }

        foreach (var profileName in profileNames)
        {
            items.Add(new TrayMenuItem($"Switch to {profileName}", TrayMenuItemKind.ActivateProfile, profileName));
        }

        items.Add(new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow));
        items.Add(new TrayMenuItem("Exit", TrayMenuItemKind.Exit));

        return new TrayMenu("SimpleHost", items);
    }

    private static TrayMenuItem ToggleFor(CategoryGroup category)
    {
        var display = category.Name ?? NoCategoryLabel;

        return category.Entries.All(entry => entry.Status == HostEntryStatus.Active)
            ? new TrayMenuItem($"Disable {display}", TrayMenuItemKind.DisableCategory, category.Name)
            : new TrayMenuItem($"Enable {display}", TrayMenuItemKind.EnableCategory, category.Name);
    }
}
