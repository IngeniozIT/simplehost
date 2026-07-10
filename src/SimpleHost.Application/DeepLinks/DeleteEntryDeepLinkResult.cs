using SimpleHost.Application.Pending;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// The outcome of handling a <c>simplehost://delete</c> deep link: either the link was resolved
/// against the current entries into a confirmation-required deletion of the matching entry, or it
/// was rejected because it named no hostname or matched no entry. Because any web page can trigger a
/// link, a resolved deletion is never applied by handling alone — it is surfaced as
/// <see cref="RequiresConfirmation"/> so the entry point can prompt the user, and only an explicit
/// <see cref="RecordInto"/> after that confirmation stages the deletion as a pending change. A
/// rejected link resolves nothing and carries a <see cref="Message"/> explaining why, so the entry
/// point can surface a clear error.
/// </summary>
public sealed record DeleteEntryDeepLinkResult
{
    private DeleteEntryDeepLinkResult(bool requiresConfirmation, HostEntry? target, string message)
    {
        RequiresConfirmation = requiresConfirmation;
        Target = target;
        Message = message;
    }

    /// <summary>
    /// Whether the link resolved to a deletion that must be confirmed by the user before it is
    /// applied. <c>false</c> for a rejected link, which resolved nothing.
    /// </summary>
    public bool RequiresConfirmation { get; }

    /// <summary>
    /// The existing entry the link matched and would delete when <see cref="RequiresConfirmation"/>
    /// is <c>true</c>; otherwise <c>null</c>.
    /// </summary>
    public HostEntry? Target { get; }

    /// <summary>
    /// Empty when the link was resolved; otherwise a human-readable explanation of why it was
    /// rejected, suitable for showing to the user.
    /// </summary>
    public string Message { get; }

    /// <summary>Records a resolved deletion of <paramref name="target"/>, pending the user's confirmation.</summary>
    internal static DeleteEntryDeepLinkResult Confirmable(HostEntry target)
        => new(true, target, string.Empty);

    /// <summary>Records that the link was rejected for the reason in <paramref name="message"/>.</summary>
    internal static DeleteEntryDeepLinkResult Rejected(string message)
        => new(false, null, message);

    /// <summary>
    /// Stages this resolved deletion in <paramref name="pendingChanges"/> once the user has confirmed
    /// it — recording the matched entry's removal as a pending change.
    /// </summary>
    /// <param name="pendingChanges">The set the confirmed deletion is recorded in.</param>
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
                "A rejected delete deep link resolved no change and cannot be recorded.");
        }

        pendingChanges.DeleteEntry(Target!);
    }
}
