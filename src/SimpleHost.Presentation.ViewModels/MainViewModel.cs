using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SimpleHost.Application.Pending;
using SimpleHost.Application.UseCases.ApplyPendingChanges;
using SimpleHost.Application.UseCases.DiscardPendingChanges;
using SimpleHost.Application.UseCases.FilterEntriesByCategory;
using SimpleHost.Application.UseCases.PreviewPendingChanges;
using SimpleHost.Application.UseCases.SaveProfile;
using SimpleHost.Application.UseCases.SearchEntriesByHostname;
using SimpleHost.Application.UseCases.SearchEntriesByIpAddress;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Preview;
using SimpleHost.Domain.Profiles;

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
    private readonly SearchEntriesByHostnameUseCase _searchByHostname;
    private readonly SearchEntriesByIpAddressUseCase _searchByIpAddress;
    private readonly FilterEntriesByCategoryUseCase _filterByCategory;
    private readonly SaveProfileUseCase _saveProfile;
    private readonly PendingChangeSet _pendingChanges;
    private readonly RelayCommand _applyCommand;
    private readonly RelayCommand _discardCommand;
    private readonly RelayCommand _previewCommand;
    private bool _hasHostnameConflicts;
    private bool _hasDuplicateEntries;
    private HostEntryViewModel? _entryBeingEdited;
    private HostsFileDiff? _previewDiff;
    private string _hostnameQuery = string.Empty;
    private string _ipAddressQuery = string.Empty;
    private string? _categoryFilter;
    private bool _isCategoryFilterActive;

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
        PendingChangeSet pendingChanges)
    {
        _viewGroupedEntries = viewGroupedEntries;
        _viewHostnameConflicts = viewHostnameConflicts;
        _viewDuplicateEntries = viewDuplicateEntries;
        _previewPendingChanges = previewPendingChanges;
        _applyPendingChanges = applyPendingChanges;
        _discardPendingChanges = discardPendingChanges;
        _searchByHostname = searchByHostname;
        _searchByIpAddress = searchByIpAddress;
        _filterByCategory = filterByCategory;
        _saveProfile = saveProfile;
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
    /// The entries grouped by category for display, in file order: the "No category" group first,
    /// then each category. Rebuilt on every load and refresh. This is the underlying data — it is
    /// never narrowed by the active search or filter; <see cref="VisibleGroups"/> is.
    /// </summary>
    public ObservableCollection<CategoryGroupViewModel> Groups { get; } = new();

    /// <summary>
    /// The grouped entries the window actually renders: <see cref="Groups"/> narrowed to those
    /// matching the active <see cref="HostnameQuery"/>, <see cref="IpAddressQuery"/> and category
    /// filter. With no active filter it mirrors <see cref="Groups"/> (empty groups included); once
    /// any criterion is active it keeps only matching entries — combining the criteria as an
    /// intersection — and drops groups left with nothing to show. The underlying
    /// <see cref="Groups"/> is left unchanged, so clearing the query restores the full view.
    /// </summary>
    public ObservableCollection<CategoryGroupViewModel> VisibleGroups { get; } = new();

    /// <summary>
    /// The hostname substring the visible list is narrowed by, matched case-insensitively through
    /// the search-by-hostname use case. Blank (empty or whitespace) means no hostname narrowing.
    /// </summary>
    public string HostnameQuery
    {
        get => _hostnameQuery;
        set
        {
            if (SetProperty(ref _hostnameQuery, value))
            {
                OnFilterChanged();
            }
        }
    }

    /// <summary>
    /// The IP-address substring the visible list is narrowed by, matched case-insensitively through
    /// the search-by-IP-address use case. Blank (empty or whitespace) means no IP narrowing.
    /// </summary>
    public string IpAddressQuery
    {
        get => _ipAddressQuery;
        set
        {
            if (SetProperty(ref _ipAddressQuery, value))
            {
                OnFilterChanged();
            }
        }
    }

    /// <summary>
    /// Whether a category filter is currently narrowing the visible list. Distinct from
    /// <see cref="CategoryFilter"/> being <c>null</c>, which — while active — selects the
    /// "No category" group.
    /// </summary>
    public bool IsCategoryFilterActive
    {
        get => _isCategoryFilterActive;
        private set => SetProperty(ref _isCategoryFilterActive, value);
    }

    /// <summary>
    /// The category the visible list is narrowed to while <see cref="IsCategoryFilterActive"/>
    /// holds: a category name, or <c>null</c> for the "No category" group.
    /// </summary>
    public string? CategoryFilter
    {
        get => _categoryFilter;
        private set => SetProperty(ref _categoryFilter, value);
    }

    /// <summary>Whether any of the hostname, IP-address or category criteria is currently active.</summary>
    public bool HasActiveFilter
        => !string.IsNullOrWhiteSpace(_hostnameQuery)
            || !string.IsNullOrWhiteSpace(_ipAddressQuery)
            || _isCategoryFilterActive;

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
            _applyPendingChanges.Execute(_pendingChanges, flushDnsAfterSave: false);
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
        OnPropertyChanged(nameof(PendingChangeCount));
        OnPropertyChanged(nameof(HasPendingChanges));
        _applyCommand.RaiseCanExecuteChanged();
        _discardCommand.RaiseCanExecuteChanged();
        _previewCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Activates the category filter on <paramref name="category"/> — a category name, or
    /// <c>null</c> for the "No category" group — narrowing <see cref="VisibleGroups"/> to that
    /// category while leaving <see cref="Groups"/> unchanged.
    /// </summary>
    public void FilterByCategory(string? category)
    {
        CategoryFilter = category;
        IsCategoryFilterActive = true;
        OnFilterChanged();
    }

    /// <summary>
    /// Turns the category filter off, so <see cref="VisibleGroups"/> is no longer narrowed by
    /// category. Any active hostname or IP-address query still applies.
    /// </summary>
    public void ClearCategoryFilter()
    {
        CategoryFilter = null;
        IsCategoryFilterActive = false;
        OnFilterChanged();
    }

    private void OnFilterChanged()
    {
        OnPropertyChanged(nameof(HasActiveFilter));
        RebuildVisibleGroups();
    }

    private void RebuildGroups(IReadOnlyList<CategoryGroup> groups)
    {
        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(new CategoryGroupViewModel(group));
        }

        RebuildVisibleGroups();
    }

    private void RebuildVisibleGroups()
    {
        var allowed = ComputeAllowedEntries();
        VisibleGroups.Clear();
        foreach (var group in Groups)
        {
            if (allowed is null)
            {
                VisibleGroups.Add(group);
                continue;
            }

            var entries = group.Entries.Where(entry => allowed.Contains(ToHostEntry(entry))).ToList();
            if (entries.Count > 0)
            {
                VisibleGroups.Add(new CategoryGroupViewModel(group.Name, entries));
            }
        }
    }

    /// <summary>
    /// Builds the set of entries permitted by every active criterion by running each active
    /// search/filter use case and intersecting their results, or <see langword="null"/> when no
    /// criterion is active (meaning "show everything"). Each criterion is skipped when inactive, so
    /// a blank query never narrows the view.
    /// </summary>
    private HashSet<HostEntry>? ComputeAllowedEntries()
    {
        HashSet<HostEntry>? allowed = null;

        if (!string.IsNullOrWhiteSpace(_hostnameQuery))
        {
            allowed = Restrict(allowed, _searchByHostname.Execute(_hostnameQuery));
        }

        if (!string.IsNullOrWhiteSpace(_ipAddressQuery))
        {
            allowed = Restrict(allowed, _searchByIpAddress.Execute(_ipAddressQuery));
        }

        if (_isCategoryFilterActive)
        {
            allowed = Restrict(allowed, _filterByCategory.Execute(_categoryFilter));
        }

        return allowed;
    }

    private static HashSet<HostEntry> Restrict(HashSet<HostEntry>? current, IReadOnlyList<HostEntry> matches)
    {
        if (current is null)
        {
            return new HashSet<HostEntry>(matches);
        }

        current.IntersectWith(matches);
        return current;
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
        var replacement = new HostEntry(ipAddress, hostname, comment, entry.Status);

        if (replacement != target)
        {
            _pendingChanges.UpdateEntry(target, replacement);
        }

        if (category != CurrentCategoryOf(entry))
        {
            _pendingChanges.MoveEntry(replacement, category);
        }
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
    /// Recalls the saved profile named <paramref name="name"/> by staging the pending changes that
    /// enable every category it lists and disable every other category — including the "No category"
    /// group — so the recall goes through the same explicit apply step as any other edit rather than
    /// writing the file directly. Entries already in the target status stage nothing. A name that
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

        foreach (var group in Groups)
        {
            var status = profile.EnabledCategories.Any(
                enabled => string.Equals(enabled, group.Name, StringComparison.Ordinal))
                ? HostEntryStatus.Active
                : HostEntryStatus.Disabled;

            StageCategoryStatusChange(group.Name, status);
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
