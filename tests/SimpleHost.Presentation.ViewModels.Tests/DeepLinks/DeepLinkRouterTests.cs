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

    // --- Null arguments ---------------------------------------------------------------------

    [Fact]
    public void Throws_when_the_current_entries_are_null()
    {
        var pending = new PendingChangeSet();

        var exception = Should.Throw<ArgumentNullException>(
            () => Route("not-a-link", null!, pending, Approve));

        exception.ParamName.ShouldBe("currentEntries");
    }

    [Fact]
    public void Throws_when_the_pending_change_set_is_null()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => Route("not-a-link", Grouped(), null!, Approve));

        exception.ParamName.ShouldBe("pendingChanges");
    }

    [Fact]
    public void Throws_when_the_confirmation_callback_is_null()
    {
        var pending = new PendingChangeSet();

        var exception = Should.Throw<ArgumentNullException>(
            () => Route("not-a-link", Grouped(), pending, null!));

        exception.ParamName.ShouldBe("confirm");
    }

    // --- Outcome messages -------------------------------------------------------------------

    [Fact]
    public void Rejecting_a_non_simplehost_link_explains_that_nothing_was_staged()
    {
        var pending = new PendingChangeSet();

        var outcome = Route("not-a-link", Grouped(), pending, Approve);

        outcome.Message.ShouldBe(
            "'not-a-link' is not a valid simplehost:// link, so nothing was staged.");
    }

    [Fact]
    public void A_staged_add_link_names_the_added_hostname_in_its_message()
    {
        var pending = new PendingChangeSet();

        var outcome = Route(
            "simplehost://add?ip=127.0.0.1&hostname=web.test", Grouped(), pending, Approve);

        outcome.Message.ShouldBe("Staged adding 'web.test' as a pending change.");
    }

    [Fact]
    public void A_staged_enable_link_names_the_enabled_hostname_in_its_message()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(
            null, new HostEntry("127.0.0.1", "web.test", null, HostEntryStatus.Disabled)));

        var outcome = Route("simplehost://enable?hostname=web.test", current, pending, Approve);

        outcome.Message.ShouldBe("Staged enabling 'web.test' as a pending change.");
    }

    [Fact]
    public void Rejects_an_enable_link_that_matches_no_entry_and_stages_nothing()
    {
        var pending = new PendingChangeSet();

        var outcome = Route("simplehost://enable?hostname=absent.test", Grouped(), pending, Approve);

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Rejected);
        outcome.Action.ShouldBe(DeepLinkAction.Enable);
        pending.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void A_staged_update_link_names_the_updated_hostname_in_its_message()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route(
            "simplehost://update?hostname=web.test&ip=10.0.0.1", current, pending, Approve);

        outcome.Message.ShouldBe("Staged the update to 'web.test' as a pending change.");
    }

    [Fact]
    public void A_staged_delete_link_names_the_deleted_hostname_in_its_message()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://delete?hostname=web.test", current, pending, Approve);

        outcome.Message.ShouldBe("Staged deleting 'web.test' as a pending change.");
    }

    [Fact]
    public void A_staged_disable_link_names_the_disabled_hostname_in_its_message()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://disable?hostname=web.test", current, pending, Approve);

        outcome.Message.ShouldBe("Staged disabling 'web.test' as a pending change.");
    }

    // --- Confirmation prompts describe the change -------------------------------------------

    [Fact]
    public void Confirming_an_update_that_changes_the_ip_describes_the_new_address()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        DeepLinkConfirmationRequest? seen = null;

        Route(
            "simplehost://update?hostname=web.test&ip=10.0.0.1",
            current,
            pending,
            request => { seen = request; return false; });

        seen.ShouldNotBeNull();
        seen!.Description.ShouldBe("Update the entry 'web.test' and change its address to 10.0.0.1?");
    }

    [Fact]
    public void Confirming_an_update_that_changes_the_category_describes_the_new_category()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));
        DeepLinkConfirmationRequest? seen = null;

        Route(
            "simplehost://update?hostname=web.test&category=Personal",
            current,
            pending,
            request => { seen = request; return false; });

        seen.ShouldNotBeNull();
        seen!.Description.ShouldBe(
            "Update the entry 'web.test' and move it to the 'Personal' category?");
    }

    [Fact]
    public void Confirming_an_update_that_changes_both_ip_and_category_joins_the_two_changes()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));
        DeepLinkConfirmationRequest? seen = null;

        Route(
            "simplehost://update?hostname=web.test&ip=10.0.0.1&category=Personal",
            current,
            pending,
            request => { seen = request; return false; });

        seen.ShouldNotBeNull();
        seen!.Description.ShouldBe(
            "Update the entry 'web.test' and change its address to 10.0.0.1 "
            + "and move it to the 'Personal' category?");
    }

    [Fact]
    public void Confirming_a_delete_describes_the_entry_to_remove()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        DeepLinkConfirmationRequest? seen = null;

        Route(
            "simplehost://delete?hostname=web.test",
            current,
            pending,
            request => { seen = request; return false; });

        seen.ShouldNotBeNull();
        seen!.Description.ShouldBe("Delete the entry mapping web.test to 127.0.0.1?");
    }

    [Fact]
    public void Confirming_a_disable_describes_the_entry_to_disable()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        DeepLinkConfirmationRequest? seen = null;

        Route(
            "simplehost://disable?hostname=web.test",
            current,
            pending,
            request => { seen = request; return false; });

        seen.ShouldNotBeNull();
        seen!.Description.ShouldBe("Disable the entry mapping web.test to 127.0.0.1?");
    }

    // --- Declined destructive links ---------------------------------------------------------

    [Fact]
    public void A_declined_update_explains_that_nothing_was_staged()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route(
            "simplehost://update?hostname=web.test&ip=10.0.0.1", current, pending, Decline);

        outcome.Message.ShouldBe(
            "The update of 'web.test' was not confirmed, so nothing was staged.");
    }

    [Fact]
    public void A_declined_delete_explains_that_nothing_was_staged()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://delete?hostname=web.test", current, pending, Decline);

        outcome.Message.ShouldBe(
            "The deletion of 'web.test' was not confirmed, so nothing was staged.");
    }

    [Fact]
    public void A_declined_disable_explains_that_nothing_was_staged()
    {
        var pending = new PendingChangeSet();
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var outcome = Route("simplehost://disable?hostname=web.test", current, pending, Decline);

        outcome.Message.ShouldBe(
            "The disabling of 'web.test' was not confirmed, so nothing was staged.");
    }

    // --- Destructive links that resolve nothing skip confirmation ---------------------------

    [Fact]
    public void Rejects_a_delete_link_that_matches_no_entry_without_asking_for_confirmation()
    {
        var pending = new PendingChangeSet();
        var confirmationAsked = false;

        var outcome = Route(
            "simplehost://delete?hostname=absent.test",
            Grouped(),
            pending,
            _ => { confirmationAsked = true; return true; });

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Rejected);
        outcome.Action.ShouldBe(DeepLinkAction.Delete);
        confirmationAsked.ShouldBeFalse();
        pending.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_disable_link_that_matches_no_entry_without_asking_for_confirmation()
    {
        var pending = new PendingChangeSet();
        var confirmationAsked = false;

        var outcome = Route(
            "simplehost://disable?hostname=absent.test",
            Grouped(),
            pending,
            _ => { confirmationAsked = true; return true; });

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.Rejected);
        outcome.Action.ShouldBe(DeepLinkAction.Disable);
        confirmationAsked.ShouldBeFalse();
        pending.HasChanges.ShouldBeFalse();
    }
}
