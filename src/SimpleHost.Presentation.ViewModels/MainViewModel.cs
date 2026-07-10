using System.Collections.ObjectModel;
using System.Windows.Input;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;

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
    private bool _hasHostnameConflicts;
    private bool _hasDuplicateEntries;

    public MainViewModel(
        ViewGroupedEntriesUseCase viewGroupedEntries,
        ViewHostnameConflictsUseCase viewHostnameConflicts,
        ViewDuplicateEntriesUseCase viewDuplicateEntries)
    {
        _viewGroupedEntries = viewGroupedEntries;
        _viewHostnameConflicts = viewHostnameConflicts;
        _viewDuplicateEntries = viewDuplicateEntries;
        RefreshCommand = new RelayCommand(Refresh);
    }

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
}
