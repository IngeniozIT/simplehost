using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.UpdateEntryComment;

/// <summary>
/// Sets, changes, or clears the trailing inline comment of an existing host entry, persisting
/// the change through the repository so the entry's <c>hosts</c>-file line is rewritten and the
/// new comment appears in the list. The entry keeps its IP address, hostname, and active/disabled
/// status.
/// </summary>
public sealed class UpdateEntryCommentUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces <paramref name="entry"/> with a copy carrying <paramref name="newComment"/> in
    /// place of its current inline comment, stores the change, and returns the updated entry.
    /// </summary>
    /// <param name="entry">The existing entry whose inline comment is being changed.</param>
    /// <param name="newComment">
    /// The inline comment the entry should now carry, or <c>null</c> to clear it so the entry has
    /// no trailing comment.
    /// </param>
    public HostEntry Execute(HostEntry entry, string? newComment)
    {
        var updated = entry with { Comment = newComment };
        repository.UpdateEntry(entry, updated);
        return updated;
    }
}
