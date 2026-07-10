using SimpleHost.Application.DeepLinks;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Presentation.ViewModels.DeepLinks;

/// <summary>
/// A request to confirm a destructive deep link before its change is staged. Because any web page can
/// trigger a <c>simplehost://update</c>, <c>simplehost://delete</c> or <c>simplehost://disable</c>
/// link, the router never stages one of these on the strength of the link alone — it asks the entry
/// point, through a confirmation callback carrying this request, and stages the change only when the
/// callback returns <c>true</c>. The request names the <see cref="Action"/> and the
/// <see cref="Target"/> entry it would affect and carries a ready-to-show
/// <see cref="Description"/> so the view can prompt the user without re-deriving the wording.
/// </summary>
/// <param name="Action">The destructive action the link asks to perform.</param>
/// <param name="Target">The existing entry the change would affect.</param>
/// <param name="Description">A human-readable, one-line description of the change, suitable for a
/// Yes/No prompt.</param>
public sealed record DeepLinkConfirmationRequest(
    DeepLinkAction Action,
    HostEntry Target,
    string Description);
