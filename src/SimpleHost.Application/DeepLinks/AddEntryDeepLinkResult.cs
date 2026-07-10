using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// The outcome of handling a <c>simplehost://add</c> deep link: either the described entry was
/// recorded as a pending addition for the user to review, or the link was rejected because the
/// IP address or hostname it carried was missing or malformed. Because any web page can trigger a
/// link, a rejected link records nothing and carries a <see cref="Message"/> explaining why, so the
/// entry point can surface a clear error instead of silently dropping the request.
/// </summary>
public sealed record AddEntryDeepLinkResult
{
    private AddEntryDeepLinkResult(bool isAdded, HostEntry? entry, string message)
    {
        IsAdded = isAdded;
        Entry = entry;
        Message = message;
    }

    /// <summary>Whether the entry was recorded as a pending change.</summary>
    public bool IsAdded { get; }

    /// <summary>
    /// The entry recorded as a pending addition when <see cref="IsAdded"/> is <c>true</c>;
    /// otherwise <c>null</c>.
    /// </summary>
    public HostEntry? Entry { get; }

    /// <summary>
    /// Empty when the entry was added; otherwise a human-readable explanation of why the link was
    /// rejected, suitable for showing to the user.
    /// </summary>
    public string Message { get; }

    /// <summary>Records that <paramref name="entry"/> was added as a pending change.</summary>
    internal static AddEntryDeepLinkResult Added(HostEntry entry) => new(true, entry, string.Empty);

    /// <summary>Records that the link was rejected for the reason in <paramref name="message"/>.</summary>
    internal static AddEntryDeepLinkResult Rejected(string message) => new(false, null, message);
}
