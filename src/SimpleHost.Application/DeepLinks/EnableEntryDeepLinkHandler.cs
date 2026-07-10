using System.Diagnostics.CodeAnalysis;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// Handles a parsed <c>simplehost://enable</c> deep link by resolving it against the current entries
/// into a pending re-enable of the disabled entry the link's <c>hostname</c> identifies, so the
/// change is surfaced for the user to review before anything is written to the <c>hosts</c> file.
/// Re-enabling is non-destructive — it makes a kept-but-not-resolving entry resolve again — so,
/// unlike the update, delete and disable actions, it needs no confirmation prompt; recording it as a
/// pending change is what keeps the user in control.
/// <para>
/// The matching entry is found by hostname, case-insensitively, taking the first <em>disabled</em>
/// entry in file order — an already-active entry with the same hostname is skipped so a later
/// disabled one can be enabled. A link that names no hostname, matches no entry, or matches only an
/// entry that already resolves is rejected with an explanation instead of recording a change. The
/// re-enabled entry keeps its position, category membership, IP address, hostname and inline comment;
/// only its status becomes <see cref="HostEntryStatus.Active"/>.
/// </para>
/// </summary>
public sealed class EnableEntryDeepLinkHandler
{
    /// <summary>
    /// Resolves <paramref name="link"/> against <paramref name="currentEntries"/> into a pending
    /// re-enable of the matching disabled entry, recorded in <paramref name="pendingChanges"/>, or a
    /// rejection explaining why no disabled entry could be enabled.
    /// </summary>
    /// <param name="link">The parsed enable deep link to handle.</param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the link's hostname is matched against.
    /// </param>
    /// <param name="pendingChanges">The set the pending re-enable is recorded in.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="link"/> is not an <see cref="DeepLinkAction.Enable"/> link, which
    /// is a programming error in the dispatch that routes links to their handlers.
    /// </exception>
    public EnableEntryDeepLinkResult Handle(
        DeepLink link,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(currentEntries);
        ArgumentNullException.ThrowIfNull(pendingChanges);

        if (link.Action != DeepLinkAction.Enable)
        {
            throw new ArgumentException(
                $"Expected an enable link but got a '{link.Action}' link.", nameof(link));
        }

        var hostname = link.Parameters.GetValueOrDefault("hostname");
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return EnableEntryDeepLinkResult.Rejected(
                "The enable link names no hostname, so no entry can be identified.");
        }

        if (!TryLocateDisabled(currentEntries, hostname, out var target, out var matchedActive))
        {
            return EnableEntryDeepLinkResult.Rejected(
                matchedActive
                    ? $"The entry with hostname '{hostname}' is already enabled."
                    : $"No entry with hostname '{hostname}' was found to enable.");
        }

        var enabled = target with { Status = HostEntryStatus.Active };
        pendingChanges.UpdateEntry(target, enabled);

        return EnableEntryDeepLinkResult.Enabled(enabled);
    }

    /// <summary>
    /// Finds the first disabled entry across <paramref name="currentEntries"/>, in file order, whose
    /// hostname equals <paramref name="hostname"/> case-insensitively. When no disabled entry
    /// matches, <paramref name="matchedActive"/> reports whether at least one already-active entry
    /// carried that hostname, so the caller can explain that the entry is already enabled rather than
    /// simply absent.
    /// </summary>
    private static bool TryLocateDisabled(
        IReadOnlyList<CategoryGroup> currentEntries,
        string hostname,
        [NotNullWhen(true)] out HostEntry? entry,
        out bool matchedActive)
    {
        matchedActive = false;

        foreach (var group in currentEntries)
        {
            foreach (var candidate in group.Entries)
            {
                if (!string.Equals(candidate.Hostname, hostname, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (candidate.Status == HostEntryStatus.Disabled)
                {
                    entry = candidate;
                    return true;
                }

                matchedActive = true;
            }
        }

        entry = null;
        return false;
    }
}
