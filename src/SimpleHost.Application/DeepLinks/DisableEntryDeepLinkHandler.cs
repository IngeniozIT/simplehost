using System.Diagnostics.CodeAnalysis;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// Handles a parsed <c>simplehost://disable</c> deep link by resolving it against the current entries
/// into the disable of the active entry the link's <c>hostname</c> identifies. Because any web page
/// can trigger a link, and disabling stops an entry resolving, the change is never applied by handling
/// alone — the returned <see cref="DisableEntryDeepLinkResult"/> flags that confirmation is required,
/// and the caller stages the disable only after the user confirms.
/// <para>
/// The matching entry is found by hostname, case-insensitively, taking the first <em>active</em> entry
/// in file order — an already-disabled entry with the same hostname is skipped so a later active one
/// can be disabled. A link that names no hostname, matches no entry, or matches only an entry that is
/// already disabled is rejected with an explanation instead of resolving a disable.
/// </para>
/// </summary>
public sealed class DisableEntryDeepLinkHandler
{
    /// <summary>
    /// Resolves <paramref name="link"/> against <paramref name="currentEntries"/> into a
    /// confirmation-required disable of the matching active entry, or a rejection explaining why no
    /// active entry could be disabled.
    /// </summary>
    /// <param name="link">The parsed disable deep link to handle.</param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the link's hostname is matched against.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="link"/> is not a <see cref="DeepLinkAction.Disable"/> link, which
    /// is a programming error in the dispatch that routes links to their handlers.
    /// </exception>
    public DisableEntryDeepLinkResult Handle(DeepLink link, IReadOnlyList<CategoryGroup> currentEntries)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(currentEntries);

        if (link.Action != DeepLinkAction.Disable)
        {
            throw new ArgumentException(
                $"Expected a disable link but got a '{link.Action}' link.", nameof(link));
        }

        var hostname = link.Parameters.GetValueOrDefault("hostname");
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return DisableEntryDeepLinkResult.Rejected(
                "The disable link names no hostname, so no entry can be identified.");
        }

        if (!TryLocateActive(currentEntries, hostname, out var target, out var matchedDisabled))
        {
            return DisableEntryDeepLinkResult.Rejected(
                matchedDisabled
                    ? $"The entry with hostname '{hostname}' is already disabled."
                    : $"No entry with hostname '{hostname}' was found to disable.");
        }

        return DisableEntryDeepLinkResult.Confirmable(target);
    }

    /// <summary>
    /// Finds the first active entry across <paramref name="currentEntries"/>, in file order, whose
    /// hostname equals <paramref name="hostname"/> case-insensitively. When no active entry matches,
    /// <paramref name="matchedDisabled"/> reports whether at least one already-disabled entry carried
    /// that hostname, so the caller can explain that the entry is already disabled rather than simply
    /// absent.
    /// </summary>
    private static bool TryLocateActive(
        IReadOnlyList<CategoryGroup> currentEntries,
        string hostname,
        [NotNullWhen(true)] out HostEntry? entry,
        out bool matchedDisabled)
    {
        matchedDisabled = false;

        foreach (var group in currentEntries)
        {
            foreach (var candidate in group.Entries)
            {
                if (!string.Equals(candidate.Hostname, hostname, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (candidate.Status == HostEntryStatus.Active)
                {
                    entry = candidate;
                    return true;
                }

                matchedDisabled = true;
            }
        }

        entry = null;
        return false;
    }
}
