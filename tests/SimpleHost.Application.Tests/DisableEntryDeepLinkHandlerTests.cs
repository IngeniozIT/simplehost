using Shouldly;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DisableEntryDeepLinkHandlerTests
{
    private static DeepLink Link(string link)
    {
        DeepLink.TryParse(link, out var parsed).ShouldBeTrue();
        return parsed!;
    }

    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static HostEntry Active(string ip, string hostname, string? comment = null)
        => new(ip, hostname, comment);

    private static HostEntry Disabled(string ip, string hostname, string? comment = null)
        => new(ip, hostname, comment, HostEntryStatus.Disabled);

    private static DisableEntryDeepLinkResult Handle(string link, IReadOnlyList<CategoryGroup> current)
        => new DisableEntryDeepLinkHandler().Handle(Link(link), current);

    private static UpdateEntryChange SingleUpdate(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();

    [Fact]
    public void Resolves_a_matching_disable_link_as_requiring_confirmation()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));

        var result = Handle("simplehost://disable?hostname=web.test", current);

        result.RequiresConfirmation.ShouldBeTrue();
        result.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_the_matched_active_entry_as_the_disable_target()
    {
        var target = Active("127.0.0.1", "web.test", "local box");
        var current = Grouped(Group(null, target));

        var result = Handle("simplehost://disable?hostname=web.test", current);

        result.Target.ShouldBe(target);
    }

    [Fact]
    public void Matches_the_entry_by_hostname_regardless_of_case()
    {
        var target = Active("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));

        var result = Handle("simplehost://disable?hostname=WEB.TEST", current);

        result.RequiresConfirmation.ShouldBeTrue();
        result.Target.ShouldBe(target);
    }

    [Fact]
    public void Resolves_the_first_active_entry_that_matches_in_file_order()
    {
        var first = Active("127.0.0.1", "web.test");
        var second = Active("10.0.0.9", "web.test");
        var current = Grouped(
            Group(null, first),
            Group("Work", second));

        var result = Handle("simplehost://disable?hostname=web.test", current);

        result.Target.ShouldBe(first);
    }

    [Fact]
    public void Skips_an_already_disabled_entry_to_disable_a_later_active_match()
    {
        var disabled = Disabled("127.0.0.1", "web.test");
        var active = Active("10.0.0.9", "web.test");
        var current = Grouped(Group(null, disabled, active));

        var result = Handle("simplehost://disable?hostname=web.test", current);

        result.Target.ShouldBe(active);
    }

    [Fact]
    public void Skips_earlier_non_matching_entries_within_a_group()
    {
        var other = Active("127.0.0.1", "other.test");
        var target = Active("10.0.0.1", "web.test");
        var current = Grouped(Group("Work", other, target));

        var result = Handle("simplehost://disable?hostname=web.test", current);

        result.Target.ShouldBe(target);
    }

    [Fact]
    public void Records_the_disable_as_a_single_pending_update_once_confirmed()
    {
        var target = Active("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://disable?hostname=web.test", current).RecordInto(pendingChanges);

        var update = SingleUpdate(pendingChanges);
        update.Target.ShouldBe(target);
        update.Replacement.Status.ShouldBe(HostEntryStatus.Disabled);
    }

    [Fact]
    public void Preserves_the_entry_content_when_disabling_it()
    {
        var target = Active("10.0.0.1", "api.test", "local box");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://disable?hostname=api.test", current).RecordInto(pendingChanges);

        SingleUpdate(pendingChanges).Replacement
            .ShouldBe(new HostEntry("10.0.0.1", "api.test", "local box", HostEntryStatus.Disabled));
    }

    [Fact]
    public void Handling_alone_records_nothing_until_the_disable_is_confirmed()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://disable?hostname=web.test", current);

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Stops_the_entry_resolving_when_the_pending_changes_are_previewed()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://disable?hostname=web.test", current).RecordInto(pendingChanges);

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries.ShouldHaveSingleItem()
            .Status.ShouldBe(HostEntryStatus.Disabled);
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_is_missing()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));

        var result = Handle("simplehost://disable", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Target.ShouldBeNull();
        result.Message.ShouldContain("no hostname");
    }

    [Fact]
    public void Treats_a_blank_hostname_as_missing()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));

        var result = Handle("simplehost://disable?hostname=%20%20", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("no hostname");
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_matches_no_entry()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));

        var result = Handle("simplehost://disable?hostname=absent.test", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Target.ShouldBeNull();
        result.Message.ShouldContain("absent.test");
        result.Message.ShouldContain("was found");
    }

    [Fact]
    public void Rejects_a_link_whose_only_match_is_already_disabled()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));

        var result = Handle("simplehost://disable?hostname=web.test", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Target.ShouldBeNull();
        result.Message.ShouldContain("already disabled");
    }

    [Fact]
    public void Refuses_to_handle_a_link_that_is_not_a_disable_link()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));

        var exception = Should.Throw<ArgumentException>(() => new DisableEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), current));

        exception.Message.ShouldContain("disable link");
    }

    [Fact]
    public void Refuses_to_record_a_rejected_result()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));
        var result = Handle("simplehost://disable?hostname=absent.test", current);

        var exception = Should.Throw<InvalidOperationException>(
            () => result.RecordInto(new PendingChangeSet()));

        exception.Message.ShouldContain("rejected");
    }

    [Fact]
    public void Rejects_a_null_link()
    {
        Should.Throw<ArgumentNullException>(() => new DisableEntryDeepLinkHandler().Handle(
            null!, Grouped(Group(null, Active("127.0.0.1", "web.test")))));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new DisableEntryDeepLinkHandler().Handle(
            Link("simplehost://disable?hostname=web.test"), null!));
    }

    [Fact]
    public void Rejects_recording_into_a_null_pending_change_set()
    {
        var current = Grouped(Group(null, Active("127.0.0.1", "web.test")));
        var result = Handle("simplehost://disable?hostname=web.test", current);

        Should.Throw<ArgumentNullException>(() => result.RecordInto(null!));
    }
}
