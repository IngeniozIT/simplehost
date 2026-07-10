using SimpleHost.Application.Pending;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// The outcome of handling a <c>simplehost://update</c> deep link: either the link was resolved
/// against the current entries into a confirmation-required change to the matching entry, or it was
/// rejected because it named no hostname, matched no entry, carried a malformed IP address, or
/// requested no actual change. Because any web page can trigger a link, a resolved update is never
/// applied by handling alone — it is surfaced as <see cref="RequiresConfirmation"/> so the entry
/// point can prompt the user, and only an explicit <see cref="RecordInto"/> after that confirmation
/// stages the change as pending edits. A rejected link resolves nothing and carries a
/// <see cref="Message"/> explaining why, so the entry point can surface a clear error.
/// </summary>
public sealed record UpdateEntryDeepLinkResult
{
    private UpdateEntryDeepLinkResult(
        bool requiresConfirmation,
        HostEntry? target,
        HostEntry? replacement,
        string? targetCategory,
        bool changesIpAddress,
        bool changesCategory,
        string message)
    {
        RequiresConfirmation = requiresConfirmation;
        Target = target;
        Replacement = replacement;
        TargetCategory = targetCategory;
        ChangesIpAddress = changesIpAddress;
        ChangesCategory = changesCategory;
        Message = message;
    }

    /// <summary>
    /// Whether the link resolved to a change that must be confirmed by the user before it is
    /// applied. <c>false</c> for a rejected link, which resolved nothing.
    /// </summary>
    public bool RequiresConfirmation { get; }

    /// <summary>
    /// The existing entry the link matched and would change when <see cref="RequiresConfirmation"/>
    /// is <c>true</c>; otherwise <c>null</c>.
    /// </summary>
    public HostEntry? Target { get; }

    /// <summary>
    /// The entry that would replace <see cref="Target"/> — the target carrying the link's new IP
    /// address, or an unchanged copy when only the category changes — when
    /// <see cref="RequiresConfirmation"/> is <c>true</c>; otherwise <c>null</c>.
    /// </summary>
    public HostEntry? Replacement { get; }

    /// <summary>
    /// The category the entry would end up in once the change is applied: the link's requested
    /// category when it changes, otherwise the entry's current category. <c>null</c> for a rejected
    /// link and for the "No category" region.
    /// </summary>
    public string? TargetCategory { get; }

    /// <summary>Whether applying the change would alter the entry's IP address.</summary>
    public bool ChangesIpAddress { get; }

    /// <summary>Whether applying the change would move the entry to a different category.</summary>
    public bool ChangesCategory { get; }

    /// <summary>
    /// Empty when the link was resolved; otherwise a human-readable explanation of why it was
    /// rejected, suitable for showing to the user.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Records a resolved update whose replacement is <paramref name="replacement"/>, landing in
    /// <paramref name="targetCategory"/>, changing the IP address and/or category as flagged.
    /// </summary>
    internal static UpdateEntryDeepLinkResult Confirmable(
        HostEntry target,
        HostEntry replacement,
        string? targetCategory,
        bool changesIpAddress,
        bool changesCategory)
        => new(true, target, replacement, targetCategory, changesIpAddress, changesCategory, string.Empty);

    /// <summary>Records that the link was rejected for the reason in <paramref name="message"/>.</summary>
    internal static UpdateEntryDeepLinkResult Rejected(string message)
        => new(false, null, null, null, false, false, message);

    /// <summary>
    /// Stages this resolved update in <paramref name="pendingChanges"/> once the user has confirmed
    /// it — recording the IP change as a pending update in place and, when the category changes, a
    /// pending move of the (already re-IP'd) entry into <see cref="TargetCategory"/>. The update is
    /// recorded before the move so the move locates the replacement rather than the original entry.
    /// </summary>
    /// <param name="pendingChanges">The set the confirmed change is recorded in.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result was rejected, so nothing can be recorded — the confirmation gate a
    /// destructive deep link requires.
    /// </exception>
    public void RecordInto(PendingChangeSet pendingChanges)
    {
        ArgumentNullException.ThrowIfNull(pendingChanges);

        if (!RequiresConfirmation)
        {
            throw new InvalidOperationException(
                "A rejected update deep link resolved no change and cannot be recorded.");
        }

        if (ChangesIpAddress)
        {
            pendingChanges.UpdateEntry(Target!, Replacement!);
        }

        if (ChangesCategory)
        {
            pendingChanges.MoveEntry(Replacement!, TargetCategory);
        }
    }
}
