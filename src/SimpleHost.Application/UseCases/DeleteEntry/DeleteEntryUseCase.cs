using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.DeleteEntry;

/// <summary>
/// Deletes a host entry from the <c>hosts</c> file through the repository, removing its line so
/// the entry disappears from the list. Deleting is destructive, so the caller (the UI, a deep
/// link, or the CLI companion) must obtain an explicit user confirmation before invoking this
/// use case.
/// </summary>
public sealed class DeleteEntryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Removes <paramref name="entry"/> from the <c>hosts</c> file. The entry is matched by its
    /// parsed content, so a disabled (commented-out) entry can be deleted too.
    /// </summary>
    /// <param name="entry">The existing entry to delete, as previously read from the file.</param>
    public void Execute(HostEntry entry) => repository.DeleteEntry(entry);
}
