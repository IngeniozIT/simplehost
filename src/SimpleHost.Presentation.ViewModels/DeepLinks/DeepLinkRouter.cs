using System.Diagnostics;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Presentation.ViewModels.DeepLinks;

/// <summary>
/// Routes an incoming raw <c>simplehost://</c> link to the matching existing deep-link handler and
/// stages the change it resolves to as a pending change, returning a uniform
/// <see cref="DeepLinkRoutingOutcome"/>. This is the whole "what should happen when a link arrives"
/// decision, kept in the testable view-model layer so the WPF entry point (single-instance forwarding
/// and window activation) can stay a thin shell.
/// <para>
/// Adding and enabling are non-destructive and are staged straight away. Because any web page can
/// trigger a link, the update, delete and disable actions are always gated behind a confirmation: the
/// router asks the supplied <c>confirm</c> callback and stages the change only when it returns
/// <c>true</c>, so a hostile or careless link can never silently change or remove an existing entry.
/// A link that cannot be parsed, names an unknown action, or is rejected by its handler stages
/// nothing and comes back as <see cref="DeepLinkOutcomeKind.Rejected"/> with an explanation.
/// </para>
/// </summary>
public sealed class DeepLinkRouter
{
    private readonly AddEntryDeepLinkHandler _add = new();
    private readonly UpdateEntryDeepLinkHandler _update = new();
    private readonly DeleteEntryDeepLinkHandler _delete = new();
    private readonly EnableEntryDeepLinkHandler _enable = new();
    private readonly DisableEntryDeepLinkHandler _disable = new();

    /// <summary>
    /// Parses <paramref name="link"/> and, when it is a valid <c>simplehost://</c> link, routes it to
    /// the matching handler against <paramref name="currentEntries"/>, staging any resolved change in
    /// <paramref name="pendingChanges"/>. Update, delete and disable links are staged only after
    /// <paramref name="confirm"/> returns <c>true</c>.
    /// </summary>
    /// <param name="link">The raw link text, as received from the operating system.</param>
    /// <param name="currentEntries">The current entries, grouped by category, the link is resolved against.</param>
    /// <param name="pendingChanges">The set a resolved change is staged in.</param>
    /// <param name="confirm">
    /// Asked before a destructive change is staged; returning <c>false</c> declines it and stages
    /// nothing. Never called for the non-destructive add and enable actions.
    /// </param>
    public DeepLinkRoutingOutcome Route(
        string? link,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges,
        Func<DeepLinkConfirmationRequest, bool> confirm)
    {
        ArgumentNullException.ThrowIfNull(currentEntries);
        ArgumentNullException.ThrowIfNull(pendingChanges);
        ArgumentNullException.ThrowIfNull(confirm);

        if (!DeepLink.TryParse(link, out var parsed))
        {
            return DeepLinkRoutingOutcome.Rejected(
                action: null,
                $"'{link}' is not a valid simplehost:// link, so nothing was staged.");
        }

        return parsed.Action switch
        {
            DeepLinkAction.Add => RouteAdd(parsed, pendingChanges),
            DeepLinkAction.Enable => RouteEnable(parsed, currentEntries, pendingChanges),
            DeepLinkAction.Update => RouteUpdate(parsed, currentEntries, pendingChanges, confirm),
            DeepLinkAction.Delete => RouteDelete(parsed, currentEntries, pendingChanges, confirm),
            DeepLinkAction.Disable => RouteDisable(parsed, currentEntries, pendingChanges, confirm),
            // DeepLink.TryParse only ever yields one of the actions handled above, so this arm is
            // unreachable; it exists solely to make the switch exhaustive for the compiler.
            _ => throw new UnreachableException(),
        };
    }

    private DeepLinkRoutingOutcome RouteAdd(DeepLink link, PendingChangeSet pendingChanges)
    {
        var result = _add.Handle(link, pendingChanges);
        return result.IsAdded
            ? DeepLinkRoutingOutcome.StagedChange(
                DeepLinkAction.Add,
                $"Staged adding '{result.Entry!.Hostname}' as a pending change.")
            : DeepLinkRoutingOutcome.Rejected(DeepLinkAction.Add, result.Message);
    }

    private DeepLinkRoutingOutcome RouteEnable(
        DeepLink link, IReadOnlyList<CategoryGroup> currentEntries, PendingChangeSet pendingChanges)
    {
        var result = _enable.Handle(link, currentEntries, pendingChanges);
        return result.IsEnabled
            ? DeepLinkRoutingOutcome.StagedChange(
                DeepLinkAction.Enable,
                $"Staged enabling '{result.Entry!.Hostname}' as a pending change.")
            : DeepLinkRoutingOutcome.Rejected(DeepLinkAction.Enable, result.Message);
    }

    private DeepLinkRoutingOutcome RouteUpdate(
        DeepLink link,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges,
        Func<DeepLinkConfirmationRequest, bool> confirm)
    {
        var result = _update.Handle(link, currentEntries);
        if (!result.RequiresConfirmation)
        {
            return DeepLinkRoutingOutcome.Rejected(DeepLinkAction.Update, result.Message);
        }

        var description = DescribeUpdate(result);
        if (!confirm(new DeepLinkConfirmationRequest(DeepLinkAction.Update, result.Target!, description)))
        {
            return Declined(DeepLinkAction.Update, result.Target!.Hostname);
        }

        result.RecordInto(pendingChanges);
        return DeepLinkRoutingOutcome.StagedChange(
            DeepLinkAction.Update,
            $"Staged the update to '{result.Target!.Hostname}' as a pending change.");
    }

    private DeepLinkRoutingOutcome RouteDelete(
        DeepLink link,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges,
        Func<DeepLinkConfirmationRequest, bool> confirm)
    {
        var result = _delete.Handle(link, currentEntries);
        if (!result.RequiresConfirmation)
        {
            return DeepLinkRoutingOutcome.Rejected(DeepLinkAction.Delete, result.Message);
        }

        var target = result.Target!;
        var description = $"Delete the entry mapping {target.Hostname} to {target.IpAddress}?";
        if (!confirm(new DeepLinkConfirmationRequest(DeepLinkAction.Delete, target, description)))
        {
            return Declined(DeepLinkAction.Delete, target.Hostname);
        }

        result.RecordInto(pendingChanges);
        return DeepLinkRoutingOutcome.StagedChange(
            DeepLinkAction.Delete,
            $"Staged deleting '{target.Hostname}' as a pending change.");
    }

    private DeepLinkRoutingOutcome RouteDisable(
        DeepLink link,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges,
        Func<DeepLinkConfirmationRequest, bool> confirm)
    {
        var result = _disable.Handle(link, currentEntries);
        if (!result.RequiresConfirmation)
        {
            return DeepLinkRoutingOutcome.Rejected(DeepLinkAction.Disable, result.Message);
        }

        var target = result.Target!;
        var description = $"Disable the entry mapping {target.Hostname} to {target.IpAddress}?";
        if (!confirm(new DeepLinkConfirmationRequest(DeepLinkAction.Disable, target, description)))
        {
            return Declined(DeepLinkAction.Disable, target.Hostname);
        }

        result.RecordInto(pendingChanges);
        return DeepLinkRoutingOutcome.StagedChange(
            DeepLinkAction.Disable,
            $"Staged disabling '{target.Hostname}' as a pending change.");
    }

    private static DeepLinkRoutingOutcome Declined(DeepLinkAction action, string hostname)
        => DeepLinkRoutingOutcome.ConfirmationDeclined(
            action, $"The {Verb(action)} of '{hostname}' was not confirmed, so nothing was staged.");

    private static string Verb(DeepLinkAction action) => action switch
    {
        DeepLinkAction.Update => "update",
        DeepLinkAction.Delete => "deletion",
        DeepLinkAction.Disable => "disabling",
        _ => action.ToString().ToLowerInvariant(),
    };

    private static string DescribeUpdate(UpdateEntryDeepLinkResult result)
    {
        var target = result.Target!;
        var changes = new List<string>();
        if (result.ChangesIpAddress)
        {
            changes.Add($"change its address to {result.Replacement!.IpAddress}");
        }

        if (result.ChangesCategory)
        {
            // An update deep link changes the category only when it carries a non-blank category
            // parameter, so TargetCategory is always a named category here — a move to "no category"
            // is not expressible through a link and therefore never reached.
            changes.Add($"move it to the '{result.TargetCategory}' category");
        }

        return $"Update the entry '{target.Hostname}' and {string.Join(" and ", changes)}?";
    }
}
