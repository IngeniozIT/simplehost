using Shouldly;
using SimpleHost.Application.Cli;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class EnableEntryCliCommandTests
{
    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static HostEntry Disabled(string ip, string hostname, string? comment = null)
        => new(ip, hostname, comment, HostEntryStatus.Disabled);

    private static UpdateEntryChange SingleUpdate(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();

    private static EnableEntryDeepLinkResult Run(
        IReadOnlyList<CategoryGroup> current,
        PendingChangeSet pendingChanges,
        params string[] args)
        => new EnableEntryCliCommand().Execute(args, current, pendingChanges);

    [Fact]
    public void Records_the_enable_command_as_a_single_pending_update()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "--hostname", "web.test");

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
    }

    [Fact]
    public void Enables_the_matched_entry_so_it_becomes_active()
    {
        var target = Disabled("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "--hostname", "web.test");

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

        Run(current, pendingChanges, "--hostname", "api.test");

        SingleUpdate(pendingChanges).Replacement
            .ShouldBe(new HostEntry("10.0.0.1", "api.test", "local box"));
    }

    [Fact]
    public void Reports_the_enabled_entry_on_the_result()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test", "local box")));
        var pendingChanges = new PendingChangeSet();

        var result = Run(current, pendingChanges, "--hostname", "web.test");

        result.IsEnabled.ShouldBeTrue();
        result.Message.ShouldBeEmpty();
        result.Entry.ShouldBe(new HostEntry("127.0.0.1", "web.test", "local box"));
    }

    [Fact]
    public void Makes_the_entry_resolve_again_when_the_pending_changes_are_previewed()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "--hostname", "web.test");

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries.ShouldHaveSingleItem()
            .Status.ShouldBe(HostEntryStatus.Active);
    }

    [Fact]
    public void Matches_the_hostname_case_insensitively()
    {
        var target = Disabled("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        var result = Run(current, pendingChanges, "--hostname", "WEB.TEST");

        result.IsEnabled.ShouldBeTrue();
        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Matches_option_names_case_insensitively()
    {
        var target = Disabled("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "--HostName", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Enables_the_first_disabled_matching_entry_in_file_order()
    {
        var first = Disabled("127.0.0.1", "web.test");
        var second = Disabled("10.0.0.9", "web.test");
        var current = Grouped(Group(null, first), Group("Work", second));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(first);
    }

    [Fact]
    public void Skips_an_already_active_entry_to_enable_a_later_disabled_match()
    {
        var active = new HostEntry("127.0.0.1", "web.test");
        var disabled = Disabled("10.0.0.9", "web.test");
        var current = Grouped(Group(null, active, disabled));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(disabled);
    }

    [Fact]
    public void Ignores_options_it_does_not_recognise()
    {
        var target = Disabled("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "--verbose", "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Ignores_a_stray_token_that_is_not_an_option()
    {
        var target = Disabled("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, "x", "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Rejects_and_records_nothing_for_a_command_with_no_options()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        var result = Run(current, pendingChanges);

        result.IsEnabled.ShouldBeFalse();
        result.Message.ShouldNotBeEmpty();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_and_records_nothing_when_the_hostname_is_blank()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        // The trailing --hostname ends the arguments, so it is blank and names no entry.
        var result = Run(current, pendingChanges, "--hostname");

        result.IsEnabled.ShouldBeFalse();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Treats_a_hostname_immediately_followed_by_another_option_as_blank()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        // The --hostname value is another option, so it is blank and names no entry.
        var result = Run(current, pendingChanges, "--hostname", "--verbose");

        result.IsEnabled.ShouldBeFalse();
        result.Message.ShouldContain("no hostname");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_and_records_nothing_when_the_hostname_matches_no_entry()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        var result = Run(current, pendingChanges, "--hostname", "absent.test");

        result.IsEnabled.ShouldBeFalse();
        result.Message.ShouldContain("absent.test");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_and_records_nothing_when_the_only_match_is_already_enabled()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        var result = Run(current, pendingChanges, "--hostname", "web.test");

        result.IsEnabled.ShouldBeFalse();
        result.Message.ShouldContain("already enabled");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Should.Throw<ArgumentNullException>(() => new EnableEntryCliCommand().Execute(
            null!,
            Grouped(Group(null, Disabled("127.0.0.1", "web.test"))),
            new PendingChangeSet()));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new EnableEntryCliCommand().Execute(
            new[] { "--hostname", "web.test" },
            null!,
            new PendingChangeSet()));
    }

    [Fact]
    public void Rejects_a_null_pending_change_set()
    {
        Should.Throw<ArgumentNullException>(() => new EnableEntryCliCommand().Execute(
            new[] { "--hostname", "web.test" },
            Grouped(Group(null, Disabled("127.0.0.1", "web.test"))),
            null!));
    }
}
