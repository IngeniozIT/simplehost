using SimpleHost.Application.DeepLinks;

namespace SimpleHost.Presentation.ViewModels.DeepLinks;

/// <summary>
/// The result of routing one incoming <c>simplehost://</c> deep link to its handler: whether the
/// change was staged, rejected, or declined at the confirmation prompt, together with the
/// <see cref="Action"/> the link asked for (when it was a valid link) and a human-readable
/// <see cref="Message"/> the entry point can surface. This turns the several per-action handler
/// results into one uniform outcome so the WPF shell stays thin.
/// </summary>
public sealed record DeepLinkRoutingOutcome
{
    private DeepLinkRoutingOutcome(DeepLinkOutcomeKind kind, DeepLinkAction? action, string message)
    {
        Kind = kind;
        Action = action;
        Message = message;
    }

    /// <summary>How routing ended.</summary>
    public DeepLinkOutcomeKind Kind { get; }

    /// <summary>
    /// The action the link asked to perform, or <c>null</c> when the link could not be parsed as a
    /// <c>simplehost://</c> deep link.
    /// </summary>
    public DeepLinkAction? Action { get; }

    /// <summary>A human-readable, one-line description of the outcome, suitable for showing to the user.</summary>
    public string Message { get; }

    /// <summary>Whether the link resolved to a change that is now staged as a pending change.</summary>
    public bool Staged => Kind == DeepLinkOutcomeKind.Staged;

    internal static DeepLinkRoutingOutcome StagedChange(DeepLinkAction action, string message)
        => new(DeepLinkOutcomeKind.Staged, action, message);

    internal static DeepLinkRoutingOutcome Rejected(DeepLinkAction? action, string message)
        => new(DeepLinkOutcomeKind.Rejected, action, message);

    internal static DeepLinkRoutingOutcome ConfirmationDeclined(DeepLinkAction action, string message)
        => new(DeepLinkOutcomeKind.ConfirmationDeclined, action, message);
}
