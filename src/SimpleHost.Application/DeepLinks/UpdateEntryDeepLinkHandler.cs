using System.Diagnostics.CodeAnalysis;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Validation;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// Handles a parsed <c>simplehost://update</c> deep link by resolving it against the current entries
/// into a change to the entry the link's <c>hostname</c> identifies: a new IP address, a new
/// category, or both. Because any web page can trigger a link, the update is never applied by
/// handling alone — the returned <see cref="UpdateEntryDeepLinkResult"/> flags that confirmation is
/// required, and the caller stages the change only after the user confirms.
/// <para>
/// The matching entry is found by hostname, case-insensitively, taking the first match in file
/// order. The <c>ip</c> and <c>category</c> parameters are optional: a present, well-formed <c>ip</c>
/// becomes the entry's new address while a malformed one is rejected and resolves nothing; a present,
/// non-blank <c>category</c> (trimmed) becomes the entry's new category. A blank parameter is treated
/// as absent, leaving that facet unchanged. The entry keeps its hostname, inline comment, and
/// active/disabled status. A link that names no hostname, matches no entry, or requests no actual
/// change is rejected with an explanation instead of resolving a change.
/// </para>
/// </summary>
public sealed class UpdateEntryDeepLinkHandler
{
    /// <summary>
    /// Resolves <paramref name="link"/> against <paramref name="currentEntries"/> into a
    /// confirmation-required update of the matching entry, or a rejection explaining why no change
    /// could be resolved.
    /// </summary>
    /// <param name="link">The parsed update deep link to handle.</param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the link's hostname is matched against.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="link"/> is not a <see cref="DeepLinkAction.Update"/> link, which
    /// is a programming error in the dispatch that routes links to their handlers.
    /// </exception>
    public UpdateEntryDeepLinkResult Handle(DeepLink link, IReadOnlyList<CategoryGroup> currentEntries)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(currentEntries);

        if (link.Action != DeepLinkAction.Update)
        {
            throw new ArgumentException(
                $"Expected an update link but got a '{link.Action}' link.", nameof(link));
        }

        var hostname = link.Parameters.GetValueOrDefault("hostname");
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return UpdateEntryDeepLinkResult.Rejected(
                "The update link names no hostname, so no entry can be identified.");
        }

        if (!TryLocate(currentEntries, hostname, out var target, out var currentCategory))
        {
            return UpdateEntryDeepLinkResult.Rejected(
                $"No entry with hostname '{hostname}' was found to update.");
        }

        var newIpAddress = target.IpAddress;
        var changesIpAddress = false;
        var ipParameter = link.Parameters.GetValueOrDefault("ip");
        if (!string.IsNullOrWhiteSpace(ipParameter))
        {
            var ipValidation = IpAddressValidation.Of(ipParameter);
            if (!ipValidation.IsWellFormed)
            {
                return UpdateEntryDeepLinkResult.Rejected(ipValidation.Message);
            }

            newIpAddress = ipParameter;
            changesIpAddress = !string.Equals(newIpAddress, target.IpAddress, StringComparison.Ordinal);
        }

        var newCategory = currentCategory;
        var changesCategory = false;
        var categoryParameter = Optional(link.Parameters.GetValueOrDefault("category"));
        if (categoryParameter is not null)
        {
            newCategory = categoryParameter;
            changesCategory = !string.Equals(newCategory, currentCategory, StringComparison.Ordinal);
        }

        if (!changesIpAddress && !changesCategory)
        {
            return UpdateEntryDeepLinkResult.Rejected(
                $"The update link requests no change to the entry '{hostname}'.");
        }

        var replacement = target with { IpAddress = newIpAddress };
        return UpdateEntryDeepLinkResult.Confirmable(
            target, replacement, newCategory, changesIpAddress, changesCategory);
    }

    /// <summary>
    /// Finds the first entry across <paramref name="currentEntries"/>, in file order, whose hostname
    /// equals <paramref name="hostname"/> case-insensitively, reporting the entry and the category it
    /// currently belongs to.
    /// </summary>
    private static bool TryLocate(
        IReadOnlyList<CategoryGroup> currentEntries,
        string hostname,
        [NotNullWhen(true)] out HostEntry? entry,
        out string? category)
    {
        foreach (var group in currentEntries)
        {
            foreach (var candidate in group.Entries)
            {
                if (string.Equals(candidate.Hostname, hostname, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    category = group.Name;
                    return true;
                }
            }
        }

        entry = null;
        category = null;
        return false;
    }

    /// <summary>
    /// Normalizes an optional link parameter: a missing or blank value becomes <c>null</c> (absent),
    /// and any other value is trimmed of surrounding whitespace.
    /// </summary>
    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
