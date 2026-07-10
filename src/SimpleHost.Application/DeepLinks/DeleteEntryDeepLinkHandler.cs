using System.Diagnostics.CodeAnalysis;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// Handles a parsed <c>simplehost://delete</c> deep link by resolving it against the current entries
/// into the deletion of the entry the link's <c>hostname</c> identifies. Because any web page can
/// trigger a link, the deletion is never applied by handling alone — the returned
/// <see cref="DeleteEntryDeepLinkResult"/> flags that confirmation is required, and the caller stages
/// the removal only after the user confirms.
/// <para>
/// The matching entry is found by hostname, case-insensitively, taking the first match in file order.
/// A link that names no hostname or matches no entry is rejected with an explanation instead of
/// resolving a deletion.
/// </para>
/// </summary>
public sealed class DeleteEntryDeepLinkHandler
{
    /// <summary>
    /// Resolves <paramref name="link"/> against <paramref name="currentEntries"/> into a
    /// confirmation-required deletion of the matching entry, or a rejection explaining why no entry
    /// could be identified.
    /// </summary>
    /// <param name="link">The parsed delete deep link to handle.</param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the link's hostname is matched against.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="link"/> is not a <see cref="DeepLinkAction.Delete"/> link, which
    /// is a programming error in the dispatch that routes links to their handlers.
    /// </exception>
    public DeleteEntryDeepLinkResult Handle(DeepLink link, IReadOnlyList<CategoryGroup> currentEntries)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(currentEntries);

        if (link.Action != DeepLinkAction.Delete)
        {
            throw new ArgumentException(
                $"Expected a delete link but got a '{link.Action}' link.", nameof(link));
        }

        var hostname = link.Parameters.GetValueOrDefault("hostname");
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return DeleteEntryDeepLinkResult.Rejected(
                "The delete link names no hostname, so no entry can be identified.");
        }

        if (!TryLocate(currentEntries, hostname, out var target))
        {
            return DeleteEntryDeepLinkResult.Rejected(
                $"No entry with hostname '{hostname}' was found to delete.");
        }

        return DeleteEntryDeepLinkResult.Confirmable(target);
    }

    /// <summary>
    /// Finds the first entry across <paramref name="currentEntries"/>, in file order, whose hostname
    /// equals <paramref name="hostname"/> case-insensitively.
    /// </summary>
    private static bool TryLocate(
        IReadOnlyList<CategoryGroup> currentEntries,
        string hostname,
        [NotNullWhen(true)] out HostEntry? entry)
    {
        foreach (var group in currentEntries)
        {
            foreach (var candidate in group.Entries)
            {
                if (string.Equals(candidate.Hostname, hostname, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }
}
