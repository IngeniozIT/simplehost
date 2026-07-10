using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.UseCases.DiscardPendingChanges;

/// <summary>
/// Discards the user's accumulated pending changes without writing anything to the <c>hosts</c>
/// file, then returns the current on-disk entries grouped by category so the view can refresh to
/// match the file exactly as it stands. This is the counterpart to
/// <see cref="ApplyPendingChanges.ApplyPendingChangesUseCase"/>: applying writes the changes and
/// then empties the set, while discarding empties the set and writes nothing, so every pending
/// edit is forgotten and the file on disk is left untouched.
/// </summary>
public sealed class DiscardPendingChangesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Empties <paramref name="pendingChanges"/> without applying any of them, then reads and
    /// returns the current grouped entries from the repository so the view can be reset to match
    /// the on-disk file. Nothing is written; discarding an empty set is a harmless no-op that
    /// simply returns the current grouping.
    /// </summary>
    /// <param name="pendingChanges">The edits the user has made but not yet written.</param>
    public IReadOnlyList<CategoryGroup> Execute(PendingChangeSet pendingChanges)
    {
        pendingChanges.Clear();
        return repository.GetGroupedEntries();
    }
}
