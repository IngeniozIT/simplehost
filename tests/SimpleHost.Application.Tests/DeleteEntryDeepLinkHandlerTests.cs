using Shouldly;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DeleteEntryDeepLinkHandlerTests
{
    private static DeepLink Link(string link)
    {
        DeepLink.TryParse(link, out var parsed).ShouldBeTrue();
        return parsed!;
    }

    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static DeleteEntryDeepLinkResult Handle(string link, IReadOnlyList<CategoryGroup> current)
        => new DeleteEntryDeepLinkHandler().Handle(Link(link), current);

    private static PendingChange SingleChange(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem();

    [Fact]
    public void Resolves_a_matching_delete_link_as_requiring_confirmation()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://delete?hostname=web.test", current);

        result.RequiresConfirmation.ShouldBeTrue();
        result.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_the_matched_entry_as_the_delete_target()
    {
        var target = new HostEntry("127.0.0.1", "web.test", "local box", HostEntryStatus.Disabled);
        var current = Grouped(Group(null, target));

        var result = Handle("simplehost://delete?hostname=web.test", current);

        result.Target.ShouldBe(target);
    }

    [Fact]
    public void Matches_the_entry_by_hostname_regardless_of_case()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));

        var result = Handle("simplehost://delete?hostname=WEB.TEST", current);

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

        var result = Handle("simplehost://delete?hostname=web.test", current);

        result.Target.ShouldBe(first);
    }

    [Fact]
    public void Skips_earlier_non_matching_entries_within_a_group()
    {
        var other = new HostEntry("127.0.0.1", "other.test");
        var target = new HostEntry("10.0.0.1", "web.test");
        var current = Grouped(Group("Work", other, target));

        var result = Handle("simplehost://delete?hostname=web.test", current);

        result.Target.ShouldBe(target);
    }

    [Fact]
    public void Records_the_deletion_as_a_single_pending_change_once_confirmed()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://delete?hostname=web.test", current).RecordInto(pendingChanges);

        SingleChange(pendingChanges).ShouldBe(new DeleteEntryChange(target));
    }

    [Fact]
    public void Handling_alone_records_nothing_until_the_deletion_is_confirmed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://delete?hostname=web.test", current);

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Removes_the_entry_when_the_pending_changes_are_previewed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://delete?hostname=web.test", current).RecordInto(pendingChanges);

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_is_missing()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://delete", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Target.ShouldBeNull();
        result.Message.ShouldContain("no hostname");
    }

    [Fact]
    public void Treats_a_blank_hostname_as_missing()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://delete?hostname=%20%20", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("no hostname");
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_matches_no_entry()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var result = Handle("simplehost://delete?hostname=absent.test", current);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Target.ShouldBeNull();
        result.Message.ShouldContain("absent.test");
    }

    [Fact]
    public void Refuses_to_handle_a_link_that_is_not_a_delete_link()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));

        var exception = Should.Throw<ArgumentException>(() => new DeleteEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), current));

        exception.Message.ShouldContain("delete link");
    }

    [Fact]
    public void Refuses_to_record_a_rejected_result()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var result = Handle("simplehost://delete?hostname=absent.test", current);

        var exception = Should.Throw<InvalidOperationException>(
            () => result.RecordInto(new PendingChangeSet()));

        exception.Message.ShouldContain("rejected");
    }

    [Fact]
    public void Rejects_a_null_link()
    {
        Should.Throw<ArgumentNullException>(() => new DeleteEntryDeepLinkHandler().Handle(
            null!, Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")))));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new DeleteEntryDeepLinkHandler().Handle(
            Link("simplehost://delete?hostname=web.test"), null!));
    }

    [Fact]
    public void Rejects_recording_into_a_null_pending_change_set()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var result = Handle("simplehost://delete?hostname=web.test", current);

        Should.Throw<ArgumentNullException>(() => result.RecordInto(null!));
    }
}
