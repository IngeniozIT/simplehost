namespace SimpleHost.Presentation.ViewModels.DeepLinks;

/// <summary>
/// How routing an incoming <c>simplehost://</c> deep link ended. Every link the running app receives
/// — whether from a fresh launch or forwarded from a second instance — resolves to exactly one of
/// these, so the entry point can react (show the pending change, or surface a clear message) without
/// re-deriving what happened.
/// </summary>
public enum DeepLinkOutcomeKind
{
    /// <summary>
    /// The link resolved to a change that is now staged as a pending change for the user to review.
    /// </summary>
    Staged,

    /// <summary>
    /// The link was understood but resolved no change — a malformed link, an unknown action, or a
    /// handler rejection (no such entry, a bad IP address, nothing to change). Nothing was staged and
    /// <see cref="DeepLinkRoutingOutcome.Message"/> explains why.
    /// </summary>
    Rejected,

    /// <summary>
    /// The link resolved to a destructive change (update, delete or disable) whose confirmation the
    /// user declined, so nothing was staged. Any web page can trigger such a link, so declining is a
    /// normal outcome rather than an error.
    /// </summary>
    ConfirmationDeclined,
}
