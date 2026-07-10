using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SimpleHost.Application.Pending;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Entries;

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
    private readonly PendingChangeSet _pendingChanges;
    private bool _hasHostnameConflicts;
    private bool _hasDuplicateEntries;
    private HostEntryViewModel? _entryBeingEdited;

    public MainViewModel(
        ViewGroupedEntriesUseCase viewGroupedEntries,
        ViewHostnameConflictsUseCase viewHostnameConflicts,
        ViewDuplicateEntriesUseCase viewDuplicateEntries,
        PendingChangeSet pendingChanges)
    {
        _viewGroupedEntries = viewGroupedEntries;
        _viewHostnameConflicts = viewHostnameConflicts;
        _viewDuplicateEntries = viewDuplicateEntries;
        _pendingChanges = pendingChanges;
        RefreshCommand = new RelayCommand(Refresh);
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
    /// then each category. Rebuilt on every load and refresh.
    /// </summary>
    public ObservableCollection<CategoryGroupViewModel> Groups { get; } = new();

    /// <summary>The human-readable hostname-conflict warnings for the current file.</summary>
    public ObservableCollection<string> HostnameConflictWarnings { get; } = new();

    /// <summary>The human-readable duplicate-entry warnings for the current file.</summary>
    public ObservableCollection<string> DuplicateEntryWarnings { get; } = new();

    /// <summary>Command that re-reads the file and refreshes the displayed state.</summary>
    public ICommand RefreshCommand { get; }

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
        Groups.Clear();
        foreach (var group in _viewGroupedEntries.Execute())
        {
            Groups.Add(new CategoryGroupViewModel(group));
        }

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

    private string? CurrentCategoryOf(HostEntryViewModel entry)
        => Groups.FirstOrDefault(group => group.Entries.Contains(entry))?.Name;

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static HostEntry ToHostEntry(HostEntryViewModel entry)
        => new(entry.IpAddress, entry.Hostname, entry.Comment, entry.Status);
}
