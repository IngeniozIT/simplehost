using SimpleHost.Application.Pending;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// The outcome of handling a <c>simplehost://disable</c> deep link: either the link was resolved
/// against the current entries into a confirmation-required disable of the matching active entry, or
/// it was rejected because it named no hostname, matched no entry, or matched only an entry that is
/// already disabled. Because any web page can trigger a link, a resolved disable is never applied by
/// handling alone — it is surfaced as <see cref="RequiresConfirmation"/> so the entry point can prompt
/// the user, and only an explicit <see cref="RecordInto"/> after that confirmation stages the change
/// as a pending update. A rejected link resolves nothing and carries a <see cref="Message"/>
/// explaining why, so the entry point can surface a clear error.
/// </summary>
public sealed record DisableEntryDeepLinkResult
{
    private DisableEntryDeepLinkResult(bool requiresConfirmation, HostEntry? target, string message)
    {
        RequiresConfirmation = requiresConfirmation;
        Target = target;
        Message = message;
    }

    /// <summary>
    /// Whether the link resolved to a disable that must be confirmed by the user before it is
    /// applied. <c>false</c> for a rejected link, which resolved nothing.
    /// </summary>
    public bool RequiresConfirmation { get; }

    /// <summary>
    /// The existing active entry the link matched and would disable when
    /// <see cref="RequiresConfirmation"/> is <c>true</c>; otherwise <c>null</c>.
    /// </summary>
    public HostEntry? Target { get; }

    /// <summary>
    /// Empty when the link was resolved; otherwise a human-readable explanation of why it was
    /// rejected, suitable for showing to the user.
    /// </summary>
    public string Message { get; }

    /// <summary>Records a resolved disable of <paramref name="target"/>, pending the user's confirmation.</summary>
    internal static DisableEntryDeepLinkResult Confirmable(HostEntry target)
        => new(true, target, string.Empty);

    /// <summary>Records that the link was rejected for the reason in <paramref name="message"/>.</summary>
    internal static DisableEntryDeepLinkResult Rejected(string message)
        => new(false, null, message);

    /// <summary>
    /// Stages this resolved disable in <paramref name="pendingChanges"/> once the user has confirmed
    /// it — recording the matched entry's replacement by a copy marked
    /// <see cref="HostEntryStatus.Disabled"/> as a pending update in place, so it keeps its position,
    /// order, category membership and content while it stops resolving.
    /// </summary>
    /// <param name="pendingChanges">The set the confirmed disable is recorded in.</param>
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
                "A rejected disable deep link resolved no change and cannot be recorded.");
        }

        var disabled = Target! with { Status = HostEntryStatus.Disabled };
        pendingChanges.UpdateEntry(Target!, disabled);
    }
}
