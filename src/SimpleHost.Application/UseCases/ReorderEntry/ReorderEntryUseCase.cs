using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.ReorderEntry;

/// <summary>
/// Reorders an existing host entry to a new position within the category it already belongs to —
/// the drag-and-drop reorder of the entry list — persisting the new order through the repository.
/// The entry's category membership and content (IP address, hostname, inline comment, and
/// active/disabled status) are preserved; only its position among its category's entries changes.
/// </summary>
public sealed class ReorderEntryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Moves <paramref name="entry"/> to the zero-based position <paramref name="targetIndex"/>
    /// among the entries of its category, persisting the change through the repository, and returns
    /// the entry unchanged.
    /// </summary>
    /// <param name="entry">The existing entry whose position is being changed.</param>
    /// <param name="targetIndex">
    /// The zero-based position the entry should occupy among its category's entries after the move.
    /// </param>
    public HostEntry Execute(HostEntry entry, int targetIndex)
    {
        repository.ReorderEntry(entry, targetIndex);
        return entry;
    }
}
