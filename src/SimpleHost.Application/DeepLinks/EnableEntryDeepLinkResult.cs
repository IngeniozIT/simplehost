using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// The outcome of handling a <c>simplehost://enable</c> deep link: either the matching disabled
/// entry was recorded as a pending change that re-enables it so it resolves again, or the link was
/// rejected because it named no hostname, matched no entry, or matched only an entry that already
/// resolves. Because any web page can trigger a link, a rejected link records nothing and carries a
/// <see cref="Message"/> explaining why, so the entry point can surface a clear error instead of
/// silently dropping the request. Re-enabling is non-destructive, so — unlike the update, delete and
/// disable actions — it needs no confirmation prompt; surfacing it as a pending change is what keeps
/// the user in control.
/// </summary>
public sealed record EnableEntryDeepLinkResult
{
    private EnableEntryDeepLinkResult(bool isEnabled, HostEntry? entry, string message)
    {
        IsEnabled = isEnabled;
        Entry = entry;
        Message = message;
    }

    /// <summary>Whether the matched entry was recorded as a pending re-enable.</summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// The now-active entry recorded as a pending change when <see cref="IsEnabled"/> is
    /// <c>true</c>; otherwise <c>null</c>.
    /// </summary>
    public HostEntry? Entry { get; }

    /// <summary>
    /// Empty when the entry was enabled; otherwise a human-readable explanation of why the link was
    /// rejected, suitable for showing to the user.
    /// </summary>
    public string Message { get; }

    /// <summary>Records that <paramref name="entry"/> was re-enabled as a pending change.</summary>
    internal static EnableEntryDeepLinkResult Enabled(HostEntry entry) => new(true, entry, string.Empty);

    /// <summary>Records that the link was rejected for the reason in <paramref name="message"/>.</summary>
    internal static EnableEntryDeepLinkResult Rejected(string message) => new(false, null, message);
}
