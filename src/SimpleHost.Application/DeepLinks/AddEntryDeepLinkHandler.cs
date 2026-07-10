using SimpleHost.Application.Pending;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Validation;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// Handles a parsed <c>simplehost://add</c> deep link by recording the entry it describes as a
/// pending addition in the user's <see cref="PendingChangeSet"/>, under the category the link names,
/// so the change is surfaced for the user to review before anything is written to the <c>hosts</c>
/// file. Adding is non-destructive, so — unlike the update, delete and disable actions — it needs no
/// confirmation prompt; surfacing it as a pending change is what keeps the user in control.
/// <para>
/// Because any web page can trigger a link, the IP address and hostname it carries are validated
/// against the same rules the editor enforces before anything is recorded: a link whose <c>ip</c> or
/// <c>hostname</c> is missing or malformed is rejected and records nothing, so a hostile or careless
/// link can never slip a broken entry into the pending changes. The <c>category</c> and
/// <c>comment</c> parameters are optional; a blank one is treated as absent.
/// </para>
/// </summary>
public sealed class AddEntryDeepLinkHandler
{
    /// <summary>
    /// Reads the <c>ip</c>, <c>hostname</c>, <c>category</c> and <c>comment</c> parameters from
    /// <paramref name="link"/> and, when the IP address and hostname are both well-formed, records a
    /// pending addition of the described active entry in <paramref name="pendingChanges"/>, returning
    /// an added result carrying the created entry. When the IP address or hostname is missing or
    /// malformed, records nothing and returns a rejected result explaining why.
    /// </summary>
    /// <param name="link">The parsed add deep link to handle.</param>
    /// <param name="pendingChanges">The set the pending addition is recorded in.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="link"/> is not an <see cref="DeepLinkAction.Add"/> link, which is a
    /// programming error in the dispatch that routes links to their handlers.
    /// </exception>
    public AddEntryDeepLinkResult Handle(DeepLink link, PendingChangeSet pendingChanges)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(pendingChanges);

        if (link.Action != DeepLinkAction.Add)
        {
            throw new ArgumentException(
                $"Expected an add link but got a '{link.Action}' link.", nameof(link));
        }

        var ipAddress = link.Parameters.GetValueOrDefault("ip");
        var ipValidation = IpAddressValidation.Of(ipAddress);
        if (!ipValidation.IsWellFormed)
        {
            return AddEntryDeepLinkResult.Rejected(ipValidation.Message);
        }

        var hostname = link.Parameters.GetValueOrDefault("hostname");
        var hostnameValidation = HostnameValidation.Of(hostname);
        if (!hostnameValidation.IsWellFormed)
        {
            return AddEntryDeepLinkResult.Rejected(hostnameValidation.Message);
        }

        var comment = Optional(link.Parameters.GetValueOrDefault("comment"));
        var category = Optional(link.Parameters.GetValueOrDefault("category"));

        var entry = new HostEntry(ipAddress!, hostname!, comment);
        pendingChanges.AddEntry(entry, category);

        return AddEntryDeepLinkResult.Added(entry);
    }

    /// <summary>
    /// Normalizes an optional link parameter: a missing or blank value becomes <c>null</c> (absent),
    /// and any other value is trimmed of surrounding whitespace.
    /// </summary>
    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
