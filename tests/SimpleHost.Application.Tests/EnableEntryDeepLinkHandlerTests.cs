using Shouldly;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class EnableEntryDeepLinkHandlerTests
{
    private static DeepLink Link(string link)
    {
        DeepLink.TryParse(link, out var parsed).ShouldBeTrue();
        return parsed!;
    }

    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static HostEntry Disabled(string ip, string hostname, string? comment = null)
        => new(ip, hostname, comment, HostEntryStatus.Disabled);

    private static EnableEntryDeepLinkResult Handle(
        string link, IReadOnlyList<CategoryGroup> current, PendingChangeSet pendingChanges)
        => new EnableEntryDeepLinkHandler().Handle(Link(link), current, pendingChanges);

    private static UpdateEntryChange SingleUpdate(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();

    [Fact]
    public void Records_the_enable_link_as_a_single_pending_update()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
    }

    [Fact]
    public void Enables_the_matched_entry_so_it_becomes_active()
    {
        var target = Disabled("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        var update = SingleUpdate(pendingChanges);
        update.Target.ShouldBe(target);
        update.Replacement.Status.ShouldBe(HostEntryStatus.Active);
    }

    [Fact]
    public void Preserves_the_entry_content_when_enabling_it()
    {
        var target = Disabled("10.0.0.1", "api.test", "local box");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://enable?hostname=api.test", current, pendingChanges);

        SingleUpdate(pendingChanges).Replacement
            .ShouldBe(new HostEntry("10.0.0.1", "api.test", "local box"));
    }

    [Fact]
    public void Surfaces_the_enabled_entry_on_the_result()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test", "local box")));
        var pendingChanges = new PendingChangeSet();

        var result = Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        result.IsEnabled.ShouldBeTrue();
        result.Message.ShouldBeEmpty();
        result.Entry.ShouldBe(new HostEntry("127.0.0.1", "web.test", "local box"));
    }

    [Fact]
    public void Makes_the_entry_resolve_again_when_the_pending_changes_are_previewed()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries.ShouldHaveSingleItem()
            .Status.ShouldBe(HostEntryStatus.Active);
    }

    [Fact]
    public void Matches_the_entry_by_hostname_regardless_of_case()
    {
        var target = Disabled("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        var result = Handle("simplehost://enable?hostname=WEB.TEST", current, pendingChanges);

        result.IsEnabled.ShouldBeTrue();
        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Enables_the_first_disabled_entry_that_matches_in_file_order()
    {
        var first = Disabled("127.0.0.1", "web.test");
        var second = Disabled("10.0.0.9", "web.test");
        var current = Grouped(
            Group(null, first),
            Group("Work", second));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        SingleUpdate(pendingChanges).Target.ShouldBe(first);
    }

    [Fact]
    public void Skips_an_already_active_entry_to_enable_a_later_disabled_match()
    {
        var active = new HostEntry("127.0.0.1", "web.test");
        var disabled = Disabled("10.0.0.9", "web.test");
        var current = Grouped(Group(null, active, disabled));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        SingleUpdate(pendingChanges).Target.ShouldBe(disabled);
    }

    [Fact]
    public void Skips_earlier_non_matching_entries_within_a_group()
    {
        var other = Disabled("127.0.0.1", "other.test");
        var target = Disabled("10.0.0.1", "web.test");
        var current = Grouped(Group("Work", other, target));
        var pendingChanges = new PendingChangeSet();

        Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_is_missing()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        var result = Handle("simplehost://enable", current, pendingChanges);

        result.IsEnabled.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        result.Message.ShouldContain("no hostname");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Treats_a_blank_hostname_as_missing()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        var result = Handle("simplehost://enable?hostname=%20%20", current, pendingChanges);

        result.IsEnabled.ShouldBeFalse();
        result.Message.ShouldContain("no hostname");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_matches_no_entry()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        var result = Handle("simplehost://enable?hostname=absent.test", current, pendingChanges);

        result.IsEnabled.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        result.Message.ShouldContain("absent.test");
        result.Message.ShouldContain("was found");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_link_whose_only_match_is_already_enabled()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        var result = Handle("simplehost://enable?hostname=web.test", current, pendingChanges);

        result.IsEnabled.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        result.Message.ShouldContain("already enabled");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Refuses_to_handle_a_link_that_is_not_an_enable_link()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));

        var exception = Should.Throw<ArgumentException>(() => new EnableEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), current, new PendingChangeSet()));

        exception.Message.ShouldContain("enable link");
    }

    [Fact]
    public void Rejects_a_null_link()
    {
        Should.Throw<ArgumentNullException>(() => new EnableEntryDeepLinkHandler().Handle(
            null!, Grouped(Group(null, Disabled("127.0.0.1", "web.test"))), new PendingChangeSet()));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new EnableEntryDeepLinkHandler().Handle(
            Link("simplehost://enable?hostname=web.test"), null!, new PendingChangeSet()));
    }

    [Fact]
    public void Rejects_a_null_pending_change_set()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));

        Should.Throw<ArgumentNullException>(() => new EnableEntryDeepLinkHandler().Handle(
            Link("simplehost://enable?hostname=web.test"), current, null!));
    }
}
