using Shouldly;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Presentation.ViewModels.DeepLinks;

namespace SimpleHost.Presentation.ViewModels.Tests.DeepLinks;

public class DeepLinkRouterTests
{
    private static readonly Func<DeepLinkConfirmationRequest, bool> Approve = _ => true;
    private static readonly Func<DeepLinkConfirmationRequest, bool> Decline = _ => false;

    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static DeepLinkRoutingOutcome Route(
        string? link,
        IReadOnlyList<CategoryGroup> current,
        PendingChangeSet pending,
        Func<DeepLinkConfirmationRequest, bool> confirm)
        => new DeepLinkRouter().Route(link, current, pending, confirm);

    // --- Parsing / unknown ------------------------------------------------------------------

    [Fact]
    public void Rejects_text_that_is_not_a_simplehost_link_and_stages_nothing()
    {
        var pending = new PendingChangeSet();

        var outcome = Route("https://example.test/add", Grouped(), pending, Approve);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Rejected);
        outcome.Action.ShouldBeNull();
        pending.HasChanges.ShouldBeFalse();
    }

    // --- Add (non-destructive, no confirmation) ---------------------------------------------

    [Fact]
    public void Stages_a_valid_add_link_as_a_pending_change_without_confirmation()
    {
        var pending = new PendingChangeSet();
        var confirmationAsked = false;

        var outcome = Route(
            "simplehost://add?ip=127.0.0.1&hostname=web.test",
            Grouped(),
            pending,
            _ => { confirmationAsked = true; return true; });

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Staged);
        outcome.Action.ShouldBe(DeepLinkAction.Add);
        pending.Changes.ShouldHaveSingleItem();
        confirmationAsked.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_an_add_link_with_a_malformed_ip_and_stages_nothing()
    {
        var pending = new PendingChangeSet();

        var outcome = Route(
            "simplehost://add?ip=not-an-ip&hostname=web.test", Grouped(), pending, Approve);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Rejected);
        outcome.Action.ShouldBe(DeepLinkAction.Add);
        pending.HasChanges.ShouldBeFalse();
    }

    // --- Enable (non-destructive, no confirmation) ------------------------------------------

    [Fact]
    public void Stages_a_valid_enable_link_without_confirmation()
    {
        var pending = new PendingChangeSet();
        var confirmationAsked = false;
        var current = Grouped(Group(
            null, new HostEntry("127.0.0.1", "web.test", null, HostEntryStatus.Disabled)));

        var outcome = Route(
            "simplehost://enable?hostname=web.test",
            current,
            pending,
            _ => { confirmationAsked = true; return true; });

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Staged);
        outcome.Action.ShouldBe(DeepLinkAction.Enable);
        pending.Changes.ShouldHaveSingleItem();
        confirmationAsked.ShouldBeFalse();
    }

    // --- Update (destructive, confirmation required) ----------------------------------------

    [Fact]
    public void Stages_a_confirmed_update_link_as_a_pending_change()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route(
            "simplehost://update?hostname=web.test&ip=10.0.0.1", current, pending, Approve);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Staged);
        outcome.Action.ShouldBe(DeepLinkAction.Update);
        pending.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void Does_not_stage_an_update_link_when_confirmation_is_declined()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route(
            "simplehost://update?hostname=web.test&ip=10.0.0.1", current, pending, Decline);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.ConfirmationDeclined);
        outcome.Action.ShouldBe(DeepLinkAction.Update);
        pending.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Asks_for_confirmation_of_an_update_naming_the_target_entry()
    {
        var pending = new PendingChangeSet();
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        DeepLinkConfirmationRequest? seen = null;

        Route(
            "simplehost://update?hostname=web.test&ip=10.0.0.1",
            current,
            pending,
            request => { seen = request; return false; });

        seen.ShouldNotBeNull();
        seen!.Action.ShouldBe(DeepLinkAction.Update);
        seen.Target.ShouldBe(target);
        seen.Description.ShouldContain("web.test");
    }

    [Fact]
    public void Rejects_an_update_link_that_matches_no_entry_without_asking_for_confirmation()
    {
        var pending = new PendingChangeSet();
        var confirmationAsked = false;

        var outcome = Route(
            "simplehost://update?hostname=absent.test&ip=10.0.0.1",
            Grouped(),
            pending,
            _ => { confirmationAsked = true; return true; });

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Rejected);
        confirmationAsked.ShouldBeFalse();
        pending.HasChanges.ShouldBeFalse();
    }

    // --- Delete (destructive, confirmation required) ----------------------------------------

    [Fact]
    public void Stages_a_confirmed_delete_link_as_a_pending_change()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://delete?hostname=web.test", current, pending, Approve);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Staged);
        outcome.Action.ShouldBe(DeepLinkAction.Delete);
        pending.Changes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Does_not_stage_a_delete_link_when_confirmation_is_declined()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://delete?hostname=web.test", current, pending, Decline);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.ConfirmationDeclined);
        pending.HasChanges.ShouldBeFalse();
    }

    // --- Disable (destructive, confirmation required) ---------------------------------------

    [Fact]
    public void Stages_a_confirmed_disable_link_as_a_pending_change()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://disable?hostname=web.test", current, pending, Approve);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Staged);
        outcome.Action.ShouldBe(DeepLinkAction.Disable);
        pending.Changes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Does_not_stage_a_disable_link_when_confirmation_is_declined()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://disable?hostname=web.test", current, pending, Decline);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.ConfirmationDeclined);
        pending.HasChanges.ShouldBeFalse();
    }
}
