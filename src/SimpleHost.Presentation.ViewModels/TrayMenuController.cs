using System;
using System.Collections.Generic;
using System.Linq;
using SimpleHost.Application.Tray;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// Wires the system tray menu to the running application. It sits between the thin WPF tray shell and
/// the <see cref="MainViewModel"/> the main window also binds to, so the tray's quick actions operate
/// on the very same application state the window shows.
///
/// <para>
/// <see cref="BuildMenu"/> reads the view-model's current categories and saved profiles and turns
/// them into a <see cref="TrayMenu"/> through the shared <see cref="TrayMenuFactory"/> — so the menu
/// always reflects what is enabled right now (a category with any disabled entry offers an "enable"
/// toggle, an all-active one offers "disable"). <see cref="Apply"/> carries a chosen category or
/// profile action out directly: it stages the change on the view-model and applies it in the same
/// step, writing to the <c>hosts</c> file at once without opening the main window. Because both the
/// tray and the window share one view-model, applying refreshes the grouped view, so an open window
/// reflects the change and a rebuilt tray menu stays in sync with the new state.
/// </para>
/// </summary>
public sealed class TrayMenuController
{
    private readonly MainViewModel _mainViewModel;
    private readonly TrayMenuFactory _trayMenuFactory;

    /// <summary>
    /// Creates a controller that drives <paramref name="mainViewModel"/> — the same view-model the
    /// main window binds to — from tray menu choices, building menus with
    /// <paramref name="trayMenuFactory"/>.
    /// </summary>
    /// <param name="mainViewModel">The application view-model the tray shares with the main window.</param>
    /// <param name="trayMenuFactory">The factory that decides the tray menu's wording and structure.</param>
    public TrayMenuController(MainViewModel mainViewModel, TrayMenuFactory trayMenuFactory)
    {
        ArgumentNullException.ThrowIfNull(mainViewModel);
        ArgumentNullException.ThrowIfNull(trayMenuFactory);

        _mainViewModel = mainViewModel;
        _trayMenuFactory = trayMenuFactory;
    }

    /// <summary>
    /// Builds the tray menu from the view-model's current state: a quick toggle for each non-empty
    /// category (with enable/disable wording matching the entries' current statuses) and a "switch to"
    /// entry for each saved profile, followed by the standing open-window and exit actions. Rebuilding
    /// it after an action is what keeps the tray in sync with the latest state.
    /// </summary>
    /// <returns>The tray menu describing the current categories, profiles and standing actions.</returns>
    public TrayMenu BuildMenu()
    {
        var categories = _mainViewModel.Groups.Select(ToCategoryGroup).ToList();
        var profileNames = _mainViewModel.Profiles.Select(profile => profile.Name).ToList();

        return _trayMenuFactory.Create(categories, profileNames);
    }

    /// <summary>
    /// Carries out the chosen tray menu <paramref name="item"/> directly against the shared
    /// view-model: an enable/disable toggle switches every entry of its category, and a profile switch
    /// recalls that profile; in each case the change is staged and applied in one step, so it is
    /// written to the <c>hosts</c> file at once. Only the category and profile quick actions are
    /// handled here — the tray shell keeps the open-window and exit gestures — so any other kind is
    /// rejected.
    /// </summary>
    /// <param name="item">The chosen menu item to carry out.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="item"/> is not one of the category or profile quick actions this
    /// controller handles.
    /// </exception>
    public void Apply(TrayMenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        switch (item.Kind)
        {
            case TrayMenuItemKind.EnableCategory:
                ApplyDirectly(() => _mainViewModel.EnableCategory(item.Target));
                break;
            case TrayMenuItemKind.DisableCategory:
                ApplyDirectly(() => _mainViewModel.DisableCategory(item.Target));
                break;
            case TrayMenuItemKind.ActivateProfile:
                ApplyDirectly(() => _mainViewModel.ActivateProfile(item.Target!));
                break;
            default:
                throw new NotSupportedException($"Unsupported tray action: {item.Kind}.");
        }
    }

    private void ApplyDirectly(Action stageChange)
    {
        stageChange();
        _mainViewModel.ApplyPendingChanges();
    }

    private static CategoryGroup ToCategoryGroup(CategoryGroupViewModel group)
        => new(group.Name, group.Entries.Select(ToHostEntry).ToList());

    private static HostEntry ToHostEntry(HostEntryViewModel entry)
        => new(entry.IpAddress, entry.Hostname, entry.Comment, entry.Status);
}
