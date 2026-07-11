using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SimpleHost.Application.Pending;
using SimpleHost.Application.UseCases.ApplyPendingChanges;
using SimpleHost.Application.UseCases.DiscardPendingChanges;
using SimpleHost.Application.UseCases.ExportCategory;
using SimpleHost.Application.UseCases.ExportConfiguration;
using SimpleHost.Application.UseCases.FilterEntriesByCategory;
using SimpleHost.Application.UseCases.ImportHostsFile;
using SimpleHost.Application.UseCases.PreviewPendingChanges;
using SimpleHost.Application.UseCases.RefreshSubscription;
using SimpleHost.Application.UseCases.RefreshSubscriptions;
using SimpleHost.Application.UseCases.SaveProfile;
using SimpleHost.Application.UseCases.SearchEntriesByHostname;
using SimpleHost.Application.UseCases.SearchEntriesByIpAddress;
using SimpleHost.Application.UseCases.SubscribeToRemoteHostsList;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Preview;
using SimpleHost.Domain.Profiles;
using SimpleHost.Domain.Validation;
using SimpleHost.Presentation.ViewModels.DeepLinks;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// The view-model the main window binds to. On load and on refresh it reads the current
/// <c>hosts</c> file through the view use cases and exposes an observable grouped representation of
/// its entries — the "No category" group pinned first, then each category — along with the current
/// hostname-conflict and duplicate-entry warnings. Refreshing re-reads the file so the displayed
/// state matches it after changes are applied.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly ViewGroupedEntriesUseCase _viewGroupedEntries;
    private readonly ViewHostnameConflictsUseCase _viewHostnameConflicts;
    private readonly ViewDuplicateEntriesUseCase _viewDuplicateEntries;
    private readonly PreviewPendingChangesUseCase _previewPendingChanges;
    private readonly ApplyPendingChangesUseCase _applyPendingChanges;
    private readonly DiscardPendingChangesUseCase _discardPendingChanges;
    private readonly SaveProfileUseCase _saveProfile;
    private readonly SubscribeToRemoteHostsListUseCase _subscribeToRemoteList;
    private readonly RefreshSubscriptionUseCase _refreshSubscription;
    private readonly RefreshSubscriptionsUseCase _refreshSubscriptions;
    private readonly ExportCategoryUseCase _exportCategory;
    private readonly ExportConfigurationUseCase _exportConfiguration;
    private readonly ImportHostsFileUseCase _importHostsFile;
    private readonly PendingChangeSet _pendingChanges;
    private readonly DeepLinkRouter _deepLinkRouter = new();
    private readonly RelayCommand _applyCommand;
    private readonly RelayCommand _discardCommand;
    private readonly RelayCommand _previewCommand;
    private IReadOnlyList<CategoryGroup> _onDiskGroups = Array.Empty<CategoryGroup>();
    private bool _hasHostnameConflicts;
    private bool _hasDuplicateEntries;
    private HostEntryViewModel? _entryBeingEdited;
    private HostsFileDiff? _previewDiff;
    private string _searchQuery = string.Empty;
    private bool _flushDnsAfterSaving;
    private bool _isAddingEntry;
    private string? _addingEntryCategory;

    public MainViewModel(
        ViewGroupedEntriesUseCase viewGroupedEntries,
        ViewHostnameConflictsUseCase viewHostnameConflicts,
        ViewDuplicateEntriesUseCase viewDuplicateEntries,
        PreviewPendingChangesUseCase previewPendingChanges,
        ApplyPendingChangesUseCase applyPendingChanges,
        DiscardPendingChangesUseCase discardPendingChanges,
        SearchEntriesByHostnameUseCase searchByHostname,
        SearchEntriesByIpAddressUseCase searchByIpAddress,
        FilterEntriesByCategoryUseCase filterByCategory,
        SaveProfileUseCase saveProfile,
        SubscribeToRemoteHostsListUseCase subscribeToRemoteList,
        RefreshSubscriptionUseCase refreshSubscription,
        RefreshSubscriptionsUseCase refreshSubscriptions,
        ExportCategoryUseCase exportCategory,
        ExportConfigurationUseCase exportConfiguration,
        ImportHostsFileUseCase importHostsFile,
        PendingChangeSet pendingChanges)
    {
        _viewGroupedEntries = viewGroupedEntries;
        _viewHostnameConflicts = viewHostnameConflicts;
        _viewDuplicateEntries = viewDuplicateEntries;
        _previewPendingChanges = previewPendingChanges;
        _applyPendingChanges = applyPendingChanges;
        _discardPendingChanges = discardPendingChanges;
        // The hostname/IP/category search and filter now narrow the pending-projected view built in
        // this view-model (so staged edits stay searchable before they are applied), rather than
        // querying the on-disk file through these use cases. The parameters are kept so the wired
        // object graph the composition root assembles is unchanged.
        _ = searchByHostname;
        _ = searchByIpAddress;
        _ = filterByCategory;
        _saveProfile = saveProfile;
        _subscribeToRemoteList = subscribeToRemoteList;
        _refreshSubscription = refreshSubscription;
        _refreshSubscriptions = refreshSubscriptions;
        _exportCategory = exportCategory;
        _exportConfiguration = exportConfiguration;
        _importHostsFile = importHostsFile;
        _pendingChanges = pendingChanges;
        RefreshCommand = new RelayCommand(Refresh);
        _applyCommand = new RelayCommand(ApplyPendingChanges, () => HasPendingChanges);
        _discardCommand = new RelayCommand(DiscardPendingChanges, () => HasPendingChanges);
        _previewCommand = new RelayCommand(Preview, () => HasPendingChanges);
        _pendingChanges.Changed += OnPendingChangesChanged;
    }

    /// <summary>
    /// The confirmation the view supplies before a delete is staged. It is handed the entry the user
    /// asked to delete and returns <see langword="true"/> to proceed or <see langword="false"/> to
    /// cancel. When left unset, <see cref="DeleteEntry"/> stages nothing — deletion fails closed until
    /// the view provides a prompt, since a delete removes a mapping the user may still rely on.
    /// </summary>
    public Func<HostEntryViewModel, bool>? DeleteConfirmation { get; set; }

    /// <summary>
    /// The confirmation the view supplies before a destructive deep link (update, delete or disable)
    /// is staged. It is handed a <see cref="DeepLinkConfirmationRequest"/> describing the change and
    /// returns <see langword="true"/> to proceed or <see langword="false"/> to cancel. When left
    /// unset, <see cref="HandleDeepLink"/> declines every destructive link and stages nothing — such
    /// links fail closed until the view provides a prompt, since any web page can trigger one. The
    /// non-destructive add and enable actions never consult it.
    /// </summary>
    public Func<DeepLinkConfirmationRequest, bool>? DeepLinkConfirmation { get; set; }

    /// <summary>
    /// Handles an incoming <c>simplehost://</c> deep link received by the running app — from a fresh
    /// launch or forwarded from a second instance — by routing it against the current entries to the
    /// matching handler and staging any resolved change as a pending change surfaced in the UI. The
    /// update, delete and disable actions are staged only after <see cref="DeepLinkConfirmation"/>
    /// approves them; a link that cannot be parsed or is rejected by its handler stages nothing. The
    /// returned <see cref="DeepLinkRoutingOutcome"/> lets the view report what happened.
    /// </summary>
    /// <param name="link">The raw link text, as received from the operating system.</param>
    public DeepLinkRoutingOutcome HandleDeepLink(string? link)
    {
        var currentEntries = _viewGroupedEntries.Execute();
        var confirm = DeepLinkConfirmation ?? (_ => false);
        return _deepLinkRouter.Route(link, currentEntries, _pendingChanges, confirm);
    }

    /// <summary>
    /// The draft of the entry being added, with live IP-address and hostname validation the view
    /// binds to for inline feedback. <see cref="SaveNewEntry"/> stages it only while
    /// <see cref="EntryFormViewModel.IsValid"/> holds.
    /// </summary>
    public EntryFormViewModel NewEntry { get; } = new();

    /// <summary>
    /// The draft of the entry being edited, populated by <see cref="BeginEditEntry"/> and validated
    /// the same way as <see cref="NewEntry"/>. <see cref="SaveEditedEntry"/> stages it only while
    /// <see cref="EntryFormViewModel.IsValid"/> holds.
    /// </summary>
    public EntryFormViewModel EditDraft { get; } = new();

    /// <summary>
    /// Whether the user is currently adding an entry inline — a draft "new entry" row is open in one
    /// category, waiting to be filled in and committed. <see cref="BeginAddEntry"/> opens it;
    /// <see cref="CommitNewEntry"/> and <see cref="CancelAddEntry"/> close it. While it holds, the
    /// view shows the editable, empty draft row (bound to <see cref="NewEntry"/>) at the bottom of the
    /// <see cref="AddingEntryCategory"/> category.
    /// </summary>
    public bool IsAddingEntry
    {
        get => _isAddingEntry;
        private set => SetProperty(ref _isAddingEntry, value);
    }

    /// <summary>
    /// The category the inline "new entry" draft is being added to while <see cref="IsAddingEntry"/>
    /// holds: a category name, or <c>null</c> for the "No category" group. Being <c>null</c> here does
    /// not mean no draft is open — <see cref="IsAddingEntry"/> tells the two apart, so the view can
    /// show the draft under the "No category" group.
    /// </summary>
    public string? AddingEntryCategory
    {
        get => _addingEntryCategory;
        private set => SetProperty(ref _addingEntryCategory, value);
    }

    /// <summary>
    /// The entries grouped by category for display, in file order: the "No category" group first,
    /// then each category. Rebuilt on every load and refresh. This is the underlying data — it is
    /// never narrowed by the active search query; <see cref="VisibleGroups"/> is.
    /// </summary>
    public ObservableCollection<CategoryGroupViewModel> Groups { get; } = new();

    /// <summary>
    /// The grouped entries the window actually renders: <see cref="Groups"/> narrowed to those
    /// matching the active <see cref="SearchQuery"/>. With no active query it mirrors
    /// <see cref="Groups"/> (empty groups included); once a query is active it keeps only matching
    /// entries and drops groups left with nothing to show. The underlying <see cref="Groups"/> is
    /// left unchanged, so clearing the query restores the full view.
    /// </summary>
    public ObservableCollection<CategoryGroupViewModel> VisibleGroups { get; } = new();

    /// <summary>
    /// The single search text the visible list is narrowed by, matched case-insensitively as a
    /// substring against each shown entry's IP address, hostname OR description (the inline comment):
    /// an entry is kept when the text appears in any one of the three. Blank (empty or whitespace)
    /// means no search narrowing.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                OnFilterChanged();
            }
        }
    }

    /// <summary>Whether the search query is currently narrowing the visible list.</summary>
    public bool HasActiveFilter
        => !string.IsNullOrWhiteSpace(_searchQuery);

    /// <summary>
    /// The entries the user has currently picked out of the list — the multi-selection the bulk
    /// commands (<see cref="EnableSelectedEntries"/>, <see cref="DisableSelectedEntries"/>,
    /// <see cref="DeleteSelectedEntries"/>, <see cref="MoveSelectedEntriesToCategory"/>) act on
    /// together. The view keeps it in sync with the selected rows.
    /// </summary>
    public ObservableCollection<HostEntryViewModel> SelectedEntries { get; } = new();

    /// <summary>
    /// The saved profiles the user can recall — the named presets of enabled categories that have
    /// been saved through this view-model. <see cref="SaveProfile"/> adds to it (replacing any
    /// profile of the same name) and <see cref="ActivateProfile"/> recalls one by name.
    /// </summary>
    public ObservableCollection<Profile> Profiles { get; } = new();

    /// <summary>
    /// The outcome of the most recent automatic refresh of every subscription, one result per
    /// subscription in catalog order, so the view can reflect which subscriptions were brought up to
    /// date and which could not be. Replaced wholesale on each <see cref="RefreshAllSubscriptions"/>;
    /// empty until the first auto-refresh runs, and left empty when nothing is subscribed.
    /// </summary>
    public ObservableCollection<SubscriptionRefreshResult> SubscriptionRefreshResults { get; } = new();

    /// <summary>The human-readable hostname-conflict warnings for the current file.</summary>
    public ObservableCollection<string> HostnameConflictWarnings { get; } = new();

    /// <summary>The human-readable duplicate-entry warnings for the current file.</summary>
    public ObservableCollection<string> DuplicateEntryWarnings { get; } = new();

    /// <summary>Command that re-reads the file and refreshes the displayed state.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// Command that writes every pending change to the resolved <c>hosts</c> file. Enabled only while
    /// there are pending changes to write. See <see cref="ApplyPendingChanges"/>.
    /// </summary>
    public ICommand ApplyCommand => _applyCommand;

    /// <summary>
    /// Command that forgets every pending change and returns the view to the on-disk file. Enabled
    /// only while there are pending changes to discard. See <see cref="DiscardPendingChanges"/>.
    /// </summary>
    public ICommand DiscardCommand => _discardCommand;

    /// <summary>
    /// Command that computes the preview diff into <see cref="PreviewDiff"/>. Enabled only while there
    /// are pending changes to preview.
    /// </summary>
    public ICommand PreviewCommand => _previewCommand;

    /// <summary>
    /// The callback the view supplies to be told that a save was refused because the process lacks the
    /// administrator rights required to write the protected system <c>hosts</c> file. When
    /// <see cref="ApplyPendingChanges"/> hits that gate it leaves every change pending, writes nothing,
    /// and invokes this so the view can request elevation and let the user retry. When left unset the
    /// refusal is simply left pending, with nothing written.
    /// </summary>
    public Action? ElevationRequired { get; set; }

    /// <summary>
    /// The callback the view supplies to be told, with a human-readable message, that an on-demand
    /// subscription operation — <see cref="SubscribeToRemoteList"/> or
    /// <see cref="RefreshSubscription"/> — was refused: a blank URL or category, a category name that
    /// already exists (when subscribing) or does not exist (when refreshing), or a remote list that
    /// could not be fetched. In every such case the use case changes nothing (it validates and fetches
    /// before it writes), the displayed state is left as it was, and this is invoked so the view can
    /// report the problem. When left unset the refusal is simply swallowed, with nothing changed. The
    /// unattended, batch <see cref="RefreshAllSubscriptions"/> never routes through this — it captures
    /// each subscription's failure into <see cref="SubscriptionRefreshResults"/> instead.
    /// </summary>
    public Action<string>? SubscriptionOperationFailed { get; set; }

    /// <summary>
    /// The callback the view supplies to be told, with a human-readable message, that an import or
    /// export was refused: a blank destination or source path, or a category that does not exist
    /// (when exporting a single category or importing into one). In every such case the use case
    /// changes nothing — an export writes no file and an import stages no entries, since each
    /// validates before it acts — the displayed state is left as it was, and this is invoked so the
    /// view can report the problem. When left unset the refusal is simply swallowed, with nothing
    /// changed.
    /// </summary>
    public Action<string>? ImportExportFailed { get; set; }

    /// <summary>
    /// The app setting the view binds to for the flush-DNS-after-saving option: when it is on, a
    /// successful <see cref="ApplyPendingChanges"/> that actually wrote changes is followed by a flush
    /// of the system DNS cache — through the apply use case's existing DNS-flush capability — so the
    /// changes take effect immediately instead of waiting for cached lookups to expire. Off by default
    /// (the flush is opt-in), and raises a change notification when toggled so the view stays in sync.
    /// A refused or empty save never flushes, whatever this setting holds.
    /// </summary>
    public bool FlushDnsAfterSaving
    {
        get => _flushDnsAfterSaving;
        set => SetProperty(ref _flushDnsAfterSaving, value);
    }

    /// <summary>The number of changes the user has staged but not yet written to the file.</summary>
    public int PendingChangeCount => _pendingChanges.Changes.Count;

    /// <summary>Whether any change is currently staged and waiting to be applied or discarded.</summary>
    public bool HasPendingChanges => _pendingChanges.HasChanges;

    /// <summary>
    /// The most recently computed preview of what the <c>hosts</c> file will look like once the
    /// pending changes are applied, or <see langword="null"/> before <see cref="PreviewCommand"/> has
    /// run or once the changes have been applied or discarded. Set by <see cref="PreviewCommand"/>.
    /// </summary>
    public HostsFileDiff? PreviewDiff
    {
        get => _previewDiff;
        private set => SetProperty(ref _previewDiff, value);
    }

    /// <summary>Whether any hostname currently resolves to conflicting IP addresses.</summary>
    public bool HasHostnameConflicts
    {
        get => _hasHostnameConflicts;
        private set => SetProperty(ref _hasHostnameConflicts, value);
    }

    /// <summary>Whether any entry is currently duplicated.</summary>
    public bool HasDuplicateEntries
    {
        get => _hasDuplicateEntries;
        private set => SetProperty(ref _hasDuplicateEntries, value);
    }

    /// <summary>Performs the initial read of the file into the observable state.</summary>
    public void Load() => Refresh();

    /// <summary>
    /// Re-reads the current file through the view use cases and updates the observable state so the
    /// grouped view and warnings match the file, including after changes have been applied.
    /// </summary>
    public void Refresh()
    {
        RebuildGroups(_viewGroupedEntries.Execute());
        RefreshWarnings();
    }

    /// <summary>
    /// Writes every pending change to the resolved <c>hosts</c> file through the apply use case — which
    /// creates the automatic backup and, when the file is the protected system file, enforces the
    /// elevation gate — then refreshes the grouped view and warnings to match the saved file, leaving
    /// nothing pending. When the save is refused for lack of administrator rights nothing is written,
    /// every change stays pending, the view is left unchanged, and <see cref="ElevationRequired"/> is
    /// invoked so the view can request elevation and let the user retry.
    /// </summary>
    public void ApplyPendingChanges()
    {
        try
        {
            _applyPendingChanges.Execute(_pendingChanges, flushDnsAfterSave: _flushDnsAfterSaving);
        }
        catch (ElevationRequiredException)
        {
            ElevationRequired?.Invoke();
            return;
        }

        Refresh();
        PreviewDiff = null;
    }

    /// <summary>
    /// Forgets every pending change through the discard use case, writing nothing, and refreshes the
    /// grouped view to match the on-disk file exactly as it stands, leaving nothing pending.
    /// </summary>
    public void DiscardPendingChanges()
    {
        RebuildGroups(_discardPendingChanges.Execute(_pendingChanges));
        PreviewDiff = null;
    }

    private void Preview() => PreviewDiff = _previewPendingChanges.Execute(_pendingChanges);

    private void OnPendingChangesChanged(object? sender, EventArgs e)
    {
        RebuildDisplayedGroups();
        OnPropertyChanged(nameof(PendingChangeCount));
        OnPropertyChanged(nameof(HasPendingChanges));
        _applyCommand.RaiseCanExecuteChanged();
        _discardCommand.RaiseCanExecuteChanged();
        _previewCommand.RaiseCanExecuteChanged();
    }

    private void OnFilterChanged()
    {
        OnPropertyChanged(nameof(HasActiveFilter));
        RebuildVisibleGroups();
    }

    /// <summary>
    /// Captures <paramref name="onDiskGroups"/> as the on-disk baseline the pending changes are
    /// projected onto, then rebuilds the displayed grouping from that baseline. Called on load,
    /// refresh, apply and discard — the points at which the view-model reads the file.
    /// </summary>
    private void RebuildGroups(IReadOnlyList<CategoryGroup> onDiskGroups)
    {
        _onDiskGroups = onDiskGroups;
        RebuildDisplayedGroups();
    }

    /// <summary>
    /// Rebuilds <see cref="Groups"/> from the pending changes projected onto the on-disk baseline, so
    /// staged edits — additions, edits, status flips, moves, reorders and deletions — show in the list
    /// immediately, then re-applies the active search and filter to the fresh projection. Pending-change
    /// targets keep resolving against the on-disk baseline because the whole set is re-projected from it
    /// each time rather than layered onto the already-projected display.
    /// </summary>
    private void RebuildDisplayedGroups()
    {
        Groups.Clear();
        foreach (var group in _pendingChanges.ApplyTo(_onDiskGroups))
        {
            Groups.Add(new CategoryGroupViewModel(group));
        }

        RebuildVisibleGroups();
    }

    private void RebuildVisibleGroups()
    {
        VisibleGroups.Clear();
        foreach (var group in Groups)
        {
            if (!HasActiveFilter)
            {
                VisibleGroups.Add(group);
                continue;
            }

            var entries = group.Entries.Where(MatchesSearchQuery).ToList();
            if (entries.Count > 0)
            {
                VisibleGroups.Add(new CategoryGroupViewModel(group.Name, entries));
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="entry"/> passes the active <see cref="SearchQuery"/> — a
    /// case-insensitive substring test satisfied when the query text appears in the entry's IP
    /// address, hostname OR description (the inline comment), so the three fields are searched as a
    /// union. A blank query matches every entry.
    /// </summary>
    private bool MatchesSearchQuery(HostEntryViewModel entry)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            return true;
        }

        var query = _searchQuery.Trim();
        return entry.IpAddress.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Hostname.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (entry.Comment is not null
                && entry.Comment.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshWarnings()
    {
        var conflicts = _viewHostnameConflicts.Execute();
        HostnameConflictWarnings.Clear();
        foreach (var conflict in conflicts.Conflicts)
        {
            HostnameConflictWarnings.Add(conflict.Message);
        }

        HasHostnameConflicts = conflicts.HasConflicts;

        var duplicates = _viewDuplicateEntries.Execute();
        DuplicateEntryWarnings.Clear();
        foreach (var duplicate in duplicates.Duplicates)
        {
            DuplicateEntryWarnings.Add(duplicate.Message);
        }

        HasDuplicateEntries = duplicates.HasDuplicates;
    }

    /// <summary>
    /// Stages a new entry mapping <paramref name="hostname"/> to <paramref name="ipAddress"/> —
    /// optionally carrying <paramref name="comment"/> and placed under <paramref name="category"/>
    /// (or "No category" when it is <c>null</c>) — as a pending change. Nothing is written to the
    /// file until the pending changes are applied.
    /// </summary>
    public void AddEntry(
        string ipAddress, string hostname, string? comment = null, string? category = null)
        => _pendingChanges.AddEntry(new HostEntry(ipAddress, hostname, comment), category);

    /// <summary>
    /// Stages creating a new, empty category named <paramref name="name"/> as a pending change.
    /// Nothing is written to the file until the pending changes are applied.
    /// </summary>
    public void CreateCategory(string name) => _pendingChanges.CreateCategory(name);

    /// <summary>
    /// Stages renaming the category <paramref name="currentName"/> to <paramref name="newName"/> as
    /// a pending change, keeping its entries where they are. Nothing is written to the file until the
    /// pending changes are applied.
    /// </summary>
    public void RenameCategory(string currentName, string newName)
        => _pendingChanges.RenameCategory(currentName, newName);

    /// <summary>
    /// Stages deleting the category <paramref name="name"/> as a pending change; the entries it held
    /// fall back to "No category". Nothing is written to the file until the pending changes are
    /// applied.
    /// </summary>
    public void DeleteCategory(string name) => _pendingChanges.DeleteCategory(name);

    /// <summary>
    /// Stages moving <paramref name="entry"/> into <paramref name="category"/> (or "No category"
    /// when it is <c>null</c>) as a pending change, appended as the last entry of the target
    /// category. Moving it into the category it is already shown under stages nothing. The entry
    /// keeps its content and active-or-disabled status. Nothing is written to the file until the
    /// pending changes are applied.
    /// </summary>
    public void MoveEntry(HostEntryViewModel entry, string? category)
    {
        if (category == CurrentCategoryOf(entry))
        {
            return;
        }

        _pendingChanges.MoveEntry(ToHostEntry(entry), category);
    }

    /// <summary>
    /// Stages reordering <paramref name="entry"/> to the zero-based position
    /// <paramref name="targetIndex"/> among the entries of the category it belongs to as a pending
    /// change. Nothing is written to the file until the pending changes are applied.
    /// </summary>
    public void ReorderEntry(HostEntryViewModel entry, int targetIndex)
        => _pendingChanges.ReorderEntry(ToHostEntry(entry), targetIndex);

    /// <summary>
    /// Stages re-enabling <paramref name="entry"/> so it resolves again, recorded as an in-place
    /// replacement that keeps its position and content and only flips its status to active. An entry
    /// that is already active stages nothing. Nothing is written to the file until the pending
    /// changes are applied.
    /// </summary>
    public void EnableEntry(HostEntryViewModel entry) => StageStatusChange(entry, HostEntryStatus.Active);

    /// <summary>
    /// Stages disabling <paramref name="entry"/> so it stays in the file but no longer resolves,
    /// recorded as an in-place replacement that keeps its position and content and only flips its
    /// status to disabled. An entry that is already disabled stages nothing. Nothing is written to
    /// the file until the pending changes are applied.
    /// </summary>
    public void DisableEntry(HostEntryViewModel entry) => StageStatusChange(entry, HostEntryStatus.Disabled);

    /// <summary>
    /// Stages enabling every entry of <paramref name="category"/> (or the "No category" group when it
    /// is <c>null</c>) at once, recording an in-place replacement for each entry that is not already
    /// active. A category that has no entries, or one that is not shown, stages nothing. Nothing is
    /// written to the file until the pending changes are applied.
    /// </summary>
    public void EnableCategory(string? category) => StageCategoryStatusChange(category, HostEntryStatus.Active);

    /// <summary>
    /// Stages disabling every entry of <paramref name="category"/> (or the "No category" group when
    /// it is <c>null</c>) at once, recording an in-place replacement for each entry that is not
    /// already disabled. A category that has no entries, or one that is not shown, stages nothing.
    /// Nothing is written to the file until the pending changes are applied.
    /// </summary>
    public void DisableCategory(string? category) => StageCategoryStatusChange(category, HostEntryStatus.Disabled);

    /// <summary>
    /// Stages enabling every entry in the current <see cref="SelectedEntries"/> selection, recording
    /// an in-place replacement for each one that is not already active. Nothing is written to the
    /// file until the pending changes are applied.
    /// </summary>
    public void EnableSelectedEntries()
    {
        foreach (var entry in SelectedEntries)
        {
            StageStatusChange(entry, HostEntryStatus.Active);
        }
    }

    /// <summary>
    /// Stages disabling every entry in the current <see cref="SelectedEntries"/> selection, recording
    /// an in-place replacement for each one that is not already disabled. Nothing is written to the
    /// file until the pending changes are applied.
    /// </summary>
    public void DisableSelectedEntries()
    {
        foreach (var entry in SelectedEntries)
        {
            StageStatusChange(entry, HostEntryStatus.Disabled);
        }
    }

    /// <summary>
    /// Stages deleting every entry in the current <see cref="SelectedEntries"/> selection. Nothing is
    /// written to the file until the pending changes are applied.
    /// </summary>
    public void DeleteSelectedEntries()
    {
        foreach (var entry in SelectedEntries)
        {
            _pendingChanges.DeleteEntry(ToHostEntry(entry));
        }
    }

    /// <summary>
    /// Stages moving every entry in the current <see cref="SelectedEntries"/> selection into
    /// <paramref name="category"/> (or "No category" when it is <c>null</c>). An entry already in the
    /// target category stages nothing. Nothing is written to the file until the pending changes are
    /// applied.
    /// </summary>
    public void MoveSelectedEntriesToCategory(string? category)
    {
        foreach (var entry in SelectedEntries)
        {
            MoveEntry(entry, category);
        }
    }

    /// <summary>
    /// Stages the edits to <paramref name="entry"/> as pending changes: when its IP address,
    /// hostname or comment change, an in-place replacement is recorded; when its
    /// <paramref name="category"/> differs from the category it is currently shown under, a move into
    /// the new category is recorded (after the replacement, so the moved entry carries the edits). An
    /// edit that changes nothing stages nothing. The entry keeps its active-or-disabled status.
    /// Nothing is written to the file until the pending changes are applied.
    /// </summary>
    public void EditEntry(
        HostEntryViewModel entry, string ipAddress, string hostname, string? comment, string? category)
    {
        var target = ToHostEntry(entry);
        // Resolve the entry's current category before staging: staging the update below rebuilds the
        // displayed Groups, which would leave this view-model instance no longer found in them.
        var currentCategory = CurrentCategoryOf(entry);
        var replacement = new HostEntry(ipAddress, hostname, comment, entry.Status);

        if (replacement != target)
        {
            _pendingChanges.UpdateEntry(target, replacement);
        }

        if (category != currentCategory)
        {
            _pendingChanges.MoveEntry(replacement, category);
        }
    }

    /// <summary>
    /// Validates a single field's candidate <paramref name="value"/> exactly as inline and form
    /// editing validate it (<see cref="EntryFormViewModel"/> and the domain value objects), returning
    /// an empty string when the value is acceptable and, when it is not, the human-readable reason it
    /// is rejected. Only the IP address and hostname are validated; the comment/description field has
    /// no validation and is always acceptable.
    /// </summary>
    public static string ValidateFieldValue(HostEntryField field, string? value)
        => field switch
        {
            HostEntryField.IpAddress =>
                IpAddressValidation.Of(value) is { IsWellFormed: false } ip ? ip.Message : string.Empty,
            HostEntryField.Hostname =>
                HostnameValidation.Of(value) is { IsWellFormed: false } host ? host.Message : string.Empty,
            _ => string.Empty,
        };

    /// <summary>
    /// Stages an inline, in-row edit of a single field of <paramref name="entry"/> — its IP address,
    /// hostname or description — validated exactly as the edit form validates it
    /// (<see cref="EntryFormViewModel"/>): an ill-formed IP address or hostname is rejected and stages
    /// nothing. A committed change is staged through the same <see cref="EditEntry"/> path, keeping the
    /// entry's category and active-or-disabled status; a value that leaves the field unchanged stages
    /// nothing. Returns an accepted result when the new value is valid (whether it staged a change or
    /// was an unchanged no-op) and a rejected result carrying the validation-failure reason when it is
    /// invalid, so the view can flag the offending field in place. Nothing is written to the file
    /// until the pending changes are applied.
    /// </summary>
    public InlineFieldEditResult EditEntryField(HostEntryViewModel entry, HostEntryField field, string? value)
    {
        var error = ValidateFieldValue(field, value);
        if (error.Length > 0)
        {
            return InlineFieldEditResult.Rejected(error);
        }

        var draft = new EntryFormViewModel
        {
            IpAddress = entry.IpAddress,
            Hostname = entry.Hostname,
            Comment = entry.Comment,
        };

        switch (field)
        {
            case HostEntryField.IpAddress:
                draft.IpAddress = value ?? string.Empty;
                break;
            case HostEntryField.Hostname:
                draft.Hostname = value ?? string.Empty;
                break;
            case HostEntryField.Comment:
                draft.Comment = value;
                break;
        }

        EditEntry(
            entry,
            draft.IpAddress,
            draft.Hostname,
            NullIfBlank(draft.Comment),
            CurrentCategoryOf(entry));
        return InlineFieldEditResult.Accepted;
    }

    /// <summary>
    /// Stages the deletion of <paramref name="entry"/> as a pending change, but only once
    /// <see cref="DeleteConfirmation"/> confirms it. When no confirmation is supplied, or the view
    /// declines, nothing is staged. Nothing is written to the file until the pending changes are
    /// applied.
    /// </summary>
    public void DeleteEntry(HostEntryViewModel entry)
    {
        if (DeleteConfirmation?.Invoke(entry) != true)
        {
            return;
        }

        _pendingChanges.DeleteEntry(ToHostEntry(entry));
    }

    /// <summary>
    /// Stages the entry drafted in <see cref="NewEntry"/> as a pending addition, but only while the
    /// draft is valid — an invalid IP address or hostname stages nothing. On success the draft is
    /// cleared so the form is ready for the next entry. Nothing is written to the file until the
    /// pending changes are applied.
    /// </summary>
    public void SaveNewEntry()
    {
        if (!NewEntry.IsValid)
        {
            return;
        }

        AddEntry(NewEntry.IpAddress, NewEntry.Hostname, NullIfBlank(NewEntry.Comment), NullIfBlank(NewEntry.Category));
        NewEntry.Reset();
    }

    /// <summary>
    /// Opens an inline "new entry" draft targeted at <paramref name="category"/> (a category name, or
    /// <c>null</c> for the "No category" group): resets <see cref="NewEntry"/> to an empty draft bound
    /// to that category, so the view can show an editable, empty row at the bottom of it. Only one
    /// draft is open at a time; opening a new one replaces any draft already open, discarding whatever
    /// was typed. Nothing is staged until the draft is committed.
    /// </summary>
    public void BeginAddEntry(string? category)
    {
        NewEntry.Reset();
        NewEntry.Category = category;
        AddingEntryCategory = category;
        IsAddingEntry = true;
    }

    /// <summary>
    /// Commits the open inline "new entry" draft. An empty draft (blank IP address, hostname and
    /// description) — a row opened but never filled in — is discarded, staging nothing, and the draft
    /// closes; this returns <c>true</c>. A draft whose IP address or hostname is ill-formed stages
    /// nothing and is left open so the user can correct it; this returns <c>false</c>. A valid draft
    /// is staged as a pending add into its target category through the same <see cref="SaveNewEntry"/>
    /// path and the draft closes; this returns <c>true</c>. With no draft open this stages nothing and
    /// returns <c>false</c>. Nothing is written to the file until the pending changes are applied.
    /// </summary>
    public bool CommitNewEntry()
    {
        if (!IsAddingEntry)
        {
            return false;
        }

        if (IsBlankDraft(NewEntry))
        {
            CancelAddEntry();
            return true;
        }

        if (!NewEntry.IsValid)
        {
            return false;
        }

        SaveNewEntry();
        IsAddingEntry = false;
        AddingEntryCategory = null;
        return true;
    }

    /// <summary>
    /// Closes the open inline "new entry" draft without staging anything, discarding whatever was
    /// typed into it. Safe to call when no draft is open.
    /// </summary>
    public void CancelAddEntry()
    {
        NewEntry.Reset();
        IsAddingEntry = false;
        AddingEntryCategory = null;
    }

    private static bool IsBlankDraft(EntryFormViewModel draft)
        => string.IsNullOrWhiteSpace(draft.IpAddress)
            && string.IsNullOrWhiteSpace(draft.Hostname)
            && string.IsNullOrWhiteSpace(draft.Comment);

    /// <summary>
    /// Begins editing <paramref name="entry"/> by loading its IP address, hostname, comment and
    /// current category into <see cref="EditDraft"/>, so the view can edit those fields with inline
    /// validation and later save them through <see cref="SaveEditedEntry"/>.
    /// </summary>
    public void BeginEditEntry(HostEntryViewModel entry)
    {
        _entryBeingEdited = entry;
        EditDraft.IpAddress = entry.IpAddress;
        EditDraft.Hostname = entry.Hostname;
        EditDraft.Comment = entry.Comment;
        EditDraft.Category = CurrentCategoryOf(entry);
    }

    /// <summary>
    /// Stages the edits drafted in <see cref="EditDraft"/> against the entry that
    /// <see cref="BeginEditEntry"/> started editing, but only while the draft is valid and an edit is
    /// in progress — an invalid IP address or hostname, or no active edit, stages nothing. Nothing is
    /// written to the file until the pending changes are applied.
    /// </summary>
    public void SaveEditedEntry()
    {
        if (_entryBeingEdited is null || !EditDraft.IsValid)
        {
            return;
        }

        EditEntry(
            _entryBeingEdited,
            EditDraft.IpAddress,
            EditDraft.Hostname,
            NullIfBlank(EditDraft.Comment),
            NullIfBlank(EditDraft.Category));
    }

    private void StageStatusChange(HostEntryViewModel entry, HostEntryStatus status)
    {
        if (entry.Status == status)
        {
            return;
        }

        var target = ToHostEntry(entry);
        _pendingChanges.UpdateEntry(target, target with { Status = status });
    }

    /// <summary>
    /// Populates <see cref="Profiles"/> from the profiles already persisted — the ones offered for
    /// recall when the app starts, including the built-in <c>"Default"</c> snapshot — replacing
    /// whatever the list held. Called once at startup with the stored profiles so a saved preset or
    /// the original-file snapshot can be switched to like any profile saved during the session.
    /// </summary>
    /// <param name="profiles">The persisted profiles to offer, in the order they should appear.</param>
    public void LoadPersistedProfiles(IEnumerable<Profile> profiles)
    {
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }
    }

    /// <summary>
    /// Saves the current set of enabled categories as a named profile through the save-profile use
    /// case and lists it under <see cref="Profiles"/>, replacing any profile already stored under the
    /// same (trimmed) name. A category counts as enabled when at least one of its entries is
    /// currently active, so the profile captures which categories are switched on right now. A blank
    /// name saves nothing, since a profile has to be named to be recalled.
    /// </summary>
    public void SaveProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var profile = _saveProfile.Execute(name, CurrentlyEnabledCategories());

        var existing = Profiles.FirstOrDefault(
            saved => string.Equals(saved.Name, profile.Name, StringComparison.Ordinal));
        if (existing is not null)
        {
            Profiles.Remove(existing);
        }

        Profiles.Add(profile);
    }

    /// <summary>
    /// Recalls the saved profile named <paramref name="name"/> by staging pending changes, going
    /// through the same explicit apply step as any other edit rather than writing the file directly.
    /// A category preset stages enabling every category it lists and disabling every other one —
    /// including the "No category" group — with entries already in the target status staging nothing.
    /// A snapshot profile (such as the built-in <c>"Default"</c>) instead stages restoring the whole
    /// captured file content, reverting it to how it was when the snapshot was taken. A name that
    /// matches no saved profile stages nothing. Nothing is written to the file until the pending
    /// changes are applied.
    /// </summary>
    public void ActivateProfile(string name)
    {
        var profile = Profiles.FirstOrDefault(
            saved => string.Equals(saved.Name, name, StringComparison.Ordinal));
        if (profile is null)
        {
            return;
        }

        if (profile.RestoresSnapshot)
        {
            _pendingChanges.RestoreContent(profile.Snapshot!, profile.SnapshotGroups!);
            return;
        }

        // Snapshot the category names first: staging a change rebuilds Groups (the display now reflects
        // pending edits), so iterating Groups directly would mutate the collection mid-enumeration.
        var categories = Groups.Select(group => group.Name).ToList();
        foreach (var category in categories)
        {
            var status = profile.EnabledCategories.Any(
                enabled => string.Equals(enabled, category, StringComparison.Ordinal))
                ? HostEntryStatus.Active
                : HostEntryStatus.Disabled;

            StageCategoryStatusChange(category, status);
        }
    }

    /// <summary>
    /// Subscribes to the remote <c>hosts</c> list at <paramref name="url"/>, placing its entries into
    /// a new dedicated category named <paramref name="category"/> through the subscribe use case, then
    /// re-reads the file so the new category and its entries appear in the grouped view. Because the
    /// use case validates and fetches before it writes, a blank URL or category, a category that
    /// already exists, or a list that cannot be fetched changes nothing on disk; the displayed state is
    /// left untouched and <see cref="SubscriptionOperationFailed"/> is invoked with the reason so the
    /// view can report it.
    /// </summary>
    public void SubscribeToRemoteList(string url, string category)
        => RunSubscriptionOperation(() => _subscribeToRemoteList.Execute(url, category));

    /// <summary>
    /// Refreshes the subscription held in the category named <paramref name="category"/> on demand by
    /// re-fetching the remote <c>hosts</c> list at <paramref name="url"/> through the refresh use case,
    /// replacing that category's entries with the freshly fetched ones, then re-reads the file so the
    /// updated category appears in the grouped view. Because the use case validates and fetches before
    /// it writes, a blank URL or category, a category that does not exist, or a list that cannot be
    /// fetched changes nothing; the displayed state is left untouched and
    /// <see cref="SubscriptionOperationFailed"/> is invoked with the reason so the view can report it.
    /// </summary>
    public void RefreshSubscription(string url, string category)
        => RunSubscriptionOperation(() => _refreshSubscription.Execute(url, category));

    /// <summary>
    /// Refreshes every kept subscription in one unattended pass through the refresh-all use case —
    /// rewriting each subscription's dedicated category with the entries freshly fetched from its
    /// remote list — records the per-subscription outcome (refreshed or failed, in catalog order) into
    /// <see cref="SubscriptionRefreshResults"/>, replacing any previous results, then re-reads the file
    /// so the refreshed categories appear in the grouped view. This is the action the app's scheduler
    /// runs on each tick; one subscription's failure is captured as its own result and never stops the
    /// others, so this never surfaces through <see cref="SubscriptionOperationFailed"/>.
    /// </summary>
    public void RefreshAllSubscriptions()
    {
        var results = _refreshSubscriptions.Execute();

        SubscriptionRefreshResults.Clear();
        foreach (var result in results)
        {
            SubscriptionRefreshResults.Add(result);
        }

        Refresh();
    }

    private void RunSubscriptionOperation(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            SubscriptionOperationFailed?.Invoke(exception.Message);
            return;
        }

        Refresh();
    }

    /// <summary>
    /// Exports the category named <paramref name="category"/> — or the "No category" group when it is
    /// <c>null</c> — as a standalone <c>hosts</c> file at <paramref name="destinationPath"/> through
    /// the export-category use case. Exporting only reads the managed file, so the displayed state is
    /// unchanged. A blank destination or a category that does not exist writes nothing and is reported
    /// through <see cref="ImportExportFailed"/>.
    /// </summary>
    public void ExportCategory(string? category, string destinationPath)
        => TryRunImportExport(() => _exportCategory.Execute(category, destinationPath));

    /// <summary>
    /// Exports the whole configuration — every category and its entries — as a single standalone
    /// <c>hosts</c> file at <paramref name="destinationPath"/> through the export-configuration use
    /// case. Exporting only reads the managed file, so the displayed state is unchanged. A blank
    /// destination writes nothing and is reported through <see cref="ImportExportFailed"/>.
    /// </summary>
    public void ExportConfiguration(string destinationPath)
        => TryRunImportExport(() => _exportConfiguration.Execute(destinationPath));

    /// <summary>
    /// Imports the entries of the external <c>hosts</c> file at <paramref name="sourcePath"/> into the
    /// category named <paramref name="category"/> — or the "No category" group when it is <c>null</c> —
    /// through the import use case, then re-reads the file so the imported entries appear in the grouped
    /// view. Because the use case validates before it reads the source, a blank source path or a
    /// category that does not exist adds nothing; the displayed state is left untouched and the reason
    /// is reported through <see cref="ImportExportFailed"/>.
    /// </summary>
    public void ImportEntries(string sourcePath, string? category)
    {
        if (TryRunImportExport(() => _importHostsFile.Execute(sourcePath, category)))
        {
            Refresh();
        }
    }

    private bool TryRunImportExport(Action operation)
    {
        try
        {
            operation();
            return true;
        }
        catch (Exception exception)
        {
            ImportExportFailed?.Invoke(exception.Message);
            return false;
        }
    }

    private IReadOnlyList<string?> CurrentlyEnabledCategories()
        => Groups
            .Where(group => group.Entries.Any(entry => entry.IsActive))
            .Select(group => group.Name)
            .ToList();

    private void StageCategoryStatusChange(string? category, HostEntryStatus status)
    {
        var group = Groups.FirstOrDefault(group => group.Name == category);
        if (group is null)
        {
            return;
        }

        foreach (var entry in group.Entries)
        {
            StageStatusChange(entry, status);
        }
    }

    private string? CurrentCategoryOf(HostEntryViewModel entry)
        => Groups.FirstOrDefault(group => group.Entries.Contains(entry))?.Name;

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static HostEntry ToHostEntry(HostEntryViewModel entry)
        => new(entry.IpAddress, entry.Hostname, entry.Comment, entry.Status);
}
