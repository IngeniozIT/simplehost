using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Application.UseCases.DeleteSelectedEntries;

/// <summary>
/// Deletes every entry in a multi-selection in one action, removing each selected entry's line
/// from the <c>hosts</c> file so the whole selection disappears from the list together. Each entry
/// is matched by its parsed content, so a disabled (commented-out) entry can be deleted too. This
/// is the bulk counterpart of <see cref="DeleteEntry.DeleteEntryUseCase"/>, applying the same
/// destructive change to a whole selection — so the caller (the UI, a deep link, or the CLI
/// companion) must obtain an explicit user confirmation before invoking this use case.
/// </summary>
public sealed class DeleteSelectedEntriesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Removes each entry in <paramref name="selection"/> from the <c>hosts</c> file, deleting
    /// their lines, and returns the deleted entries in the selection's order. An empty selection
    /// changes nothing and returns no entries.
    /// </summary>
    /// <param name="selection">The entries the user picked out to delete together.</param>
    public IReadOnlyList<HostEntry> Execute(EntrySelection selection)
    {
        var deleted = new List<HostEntry>(selection.Count);

        foreach (var entry in selection.Entries)
        {
            repository.DeleteEntry(entry);
            deleted.Add(entry);
        }

        return deleted;
    }
}
