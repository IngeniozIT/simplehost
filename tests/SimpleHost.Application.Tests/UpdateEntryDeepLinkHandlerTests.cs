using Shouldly;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class UpdateEntryDeepLinkHandlerTests
{
    private static DeepLink Link(string link)
    {
        DeepLink.TryParse(link, out var parsed).ShouldBeTrue();
        return parsed!;
    }

    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static UpdateEntryDeepLinkResult Handle(string link, IReadOnlyList<CategoryGroup> current)
        => new UpdateEntryDeepLinkHandler().Handle(Link(link), current);

    private static PendingChange SingleChange(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem();

    [Fact]
    public void Resolves_a_matching_update_link_as_requiring_confirmation()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        result.RequiresConfirmation.ShouldBeTrue();
        result.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_the_matched_entry_as_the_update_target()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));

        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        result.Target.ShouldBe(target);
    }

    [Fact]
    public void Replaces_the_ip_address_while_keeping_the_hostname_in_the_replacement()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        result.Replacement.ShouldBe(new HostEntry("10.0.0.1", "web.test"));
    }

    [Fact]
    public void Preserves_the_comment_and_disabled_status_when_changing_the_ip()
    {
        var target = new HostEntry("127.0.0.1", "web.test", "local box", HostEntryStatus.Disabled);
        var current = Grouped(Group(null, target));

        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        result.Replacement.ShouldBe(
            new HostEntry("10.0.0.1", "web.test", "local box", HostEntryStatus.Disabled));
    }

    [Fact]
    public void Matches_the_entry_by_hostname_regardless_of_case()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));

        var result = Handle("simplehost://update?hostname=WEB.TEST&ip=10.0.0.1", current);

        result.RequiresConfirmation.ShouldBeTrue();
        result.Target.ShouldBe(target);
    }

    [Fact]
    public void Resolves_the_first_entry_that_matches_in_file_order()
    {
        var first = new HostEntry("127.0.0.1", "web.test");
        var second = new HostEntry("10.0.0.9", "web.test");
        var current = Grouped(
            Group(null, first),
            Group("Work", second));

        var result = Handle("simplehost://update?hostname=web.test&ip=192.168.0.1", current);

        result.Target.ShouldBe(first);
    }

    [Fact]
    public void Records_the_ip_change_as_a_single_pending_update_once_confirmed()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);
        result.RecordInto(pendingChanges);

        SingleChange(pendingChanges)
            .ShouldBe(new UpdateEntryChange(target, new HostEntry("10.0.0.1", "web.test")));
    }

    [Fact]
    public void Handling_alone_records_nothing_until_the_change_is_confirmed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Surfaces_the_updated_ip_when_the_pending_changes_are_previewed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current).RecordInto(pendingChanges);

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries
            .ShouldHaveSingleItem().ShouldBe(new HostEntry("10.0.0.1", "web.test"));
    }

    [Fact]
    public void Reports_that_the_ip_address_changes_when_a_new_ip_is_given()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        result.ChangesIpAddress.ShouldBeTrue();
        result.ChangesCategory.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_change_the_ip_when_the_given_ip_equals_the_current_one()
    {
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&ip=127.0.0.1&category=Home", current);

        result.ChangesIpAddress.ShouldBeFalse();
        result.Replacement.ShouldBe(new HostEntry("127.0.0.1", "web.test"));
    }

    [Fact]
    public void Records_only_the_ip_update_and_no_move_when_the_category_is_unchanged()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group("Work", target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://update?hostname=web.test&ip=10.0.0.1&category=Work", current)
            .RecordInto(pendingChanges);

        SingleChange(pendingChanges)
            .ShouldBe(new UpdateEntryChange(target, new HostEntry("10.0.0.1", "web.test")));
    }

    [Fact]
    public void Reports_that_the_category_changes_when_a_different_category_is_given()
    {
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&category=Home", current);

        result.ChangesCategory.ShouldBeTrue();
        result.TargetCategory.ShouldBe("Home");
    }

    [Fact]
    public void Records_only_a_move_when_just_the_category_changes()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group("Work", target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://update?hostname=web.test&category=Home", current).RecordInto(pendingChanges);

        SingleChange(pendingChanges).ShouldBe(new MoveEntryChange(target, "Home"));
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_given_category()
    {
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&category=%20Home%20", current);

        result.TargetCategory.ShouldBe("Home");
    }

    [Fact]
    public void Records_the_ip_update_before_moving_the_replacement_when_both_change()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group("Work", target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://update?hostname=web.test&ip=10.0.0.1&category=Home", current)
            .RecordInto(pendingChanges);

        var replacement = new HostEntry("10.0.0.1", "web.test");
        pendingChanges.Changes.Count.ShouldBe(2);
        pendingChanges.Changes[0].ShouldBe(new UpdateEntryChange(target, replacement));
        pendingChanges.Changes[1].ShouldBe(new MoveEntryChange(replacement, "Home"));
    }

    [Fact]
    public void Moving_and_updating_both_land_when_the_pending_changes_are_previewed()
    {
        var current = Grouped(
            Group("Work", new HostEntry("127.0.0.1", "web.test")),
            Group("Home"));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://update?hostname=web.test&ip=10.0.0.1&category=Home", current)
            .RecordInto(pendingChanges);

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name == "Work").Entries.ShouldBeEmpty();
        projected.Single(group => group.Name == "Home").Entries
            .ShouldHaveSingleItem().ShouldBe(new HostEntry("10.0.0.1", "web.test"));
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_is_missing()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?ip=10.0.0.1", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Target.ShouldBeNull();
        result.Replacement.ShouldBeNull();
        result.ChangesIpAddress.ShouldBeFalse();
        result.ChangesCategory.ShouldBeFalse();
        result.Message.ShouldContain("no hostname");
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_matches_no_entry()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=absent.test&ip=10.0.0.1", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("absent.test");
    }

    [Fact]
    public void Rejects_a_link_whose_ip_address_is_malformed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&ip=1.2.3", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("1.2.3");
    }

    [Fact]
    public void Rejects_a_link_that_requests_no_change_at_all()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("no change");
    }

    [Fact]
    public void Treats_a_blank_category_as_requesting_no_category_change()
    {
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&category=%20%20", current);

        result.RequiresConfirmation.ShouldBeFalse();
    }

    [Fact]
    public void Treats_a_blank_ip_as_requesting_no_ip_change()
    {
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&ip=%20%20", current);

        result.RequiresConfirmation.ShouldBeFalse();
    }

    [Fact]
    public void Reports_a_category_move_into_no_category_is_not_triggered_by_absence()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        result.ChangesCategory.ShouldBeFalse();
    }

    [Fact]
    public void Refuses_to_handle_a_link_that_is_not_an_update_link()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var exception = Should.Throw<ArgumentException>(() => new UpdateEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), current));

        exception.Message.ShouldContain("update link");
    }

    [Fact]
    public void Refuses_to_record_a_rejected_result()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var result = Handle("simplehost://update?hostname=absent.test&ip=10.0.0.1", current);

        var exception = Should.Throw<InvalidOperationException>(
            () => result.RecordInto(new PendingChangeSet()));

        exception.Message.ShouldContain("rejected");
    }

    [Fact]
    public void Rejects_a_null_link()
    {
        Should.Throw<ArgumentNullException>(() => new UpdateEntryDeepLinkHandler().Handle(
            null!, Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")))));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new UpdateEntryDeepLinkHandler().Handle(
            Link("simplehost://update?hostname=web.test&ip=10.0.0.1"), null!));
    }

    [Fact]
    public void Rejects_recording_into_a_null_pending_change_set()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var result = Handle("simplehost://update?hostname=web.test&ip=10.0.0.1", current);

        Should.Throw<ArgumentNullException>(() => result.RecordInto(null!));
    }
}
