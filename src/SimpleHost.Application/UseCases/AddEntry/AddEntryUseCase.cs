using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.AddEntry;

/// <summary>
/// Adds a new host entry from an IP address and a hostname, persisting it through the
/// repository so it is written into the <c>hosts</c> file and appears in the list.
/// </summary>
public sealed class AddEntryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Creates an active entry mapping <paramref name="hostname"/> to
    /// <paramref name="ipAddress"/>, optionally carrying <paramref name="comment"/> as its
    /// trailing inline comment, stores it, and returns the created entry.
    /// </summary>
    /// <param name="ipAddress">The IP address the hostname resolves to.</param>
    /// <param name="hostname">The hostname being mapped.</param>
    /// <param name="comment">
    /// An optional trailing inline comment to note what the entry is for, or <c>null</c> to
    /// create the entry without a comment.
    /// </param>
    /// <param name="category">
    /// The name of an existing category to place the entry under, or <c>null</c> to leave it
    /// uncategorized ("No category").
    /// </param>
    public HostEntry Execute(
        string ipAddress, string hostname, string? comment = null, string? category = null)
    {
        var entry = new HostEntry(ipAddress, hostname, comment);
        repository.AddEntry(entry, category);
        return entry;
    }
}
