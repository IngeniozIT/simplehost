using Shouldly;
using SimpleHost.Application.Cli;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DisableEntryCliCommandTests
{
    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static HostEntry Disabled(string ip, string hostname, string? comment = null)
        => new(ip, hostname, comment, HostEntryStatus.Disabled);

    private static UpdateEntryChange SingleUpdate(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();

    /// <summary>A confirmation stub that records whether it was asked and answers a fixed way.</summary>
    private sealed class ConfirmationStub
    {
        private readonly bool _answer;

        public ConfirmationStub(bool answer) => _answer = answer;

        public int TimesAsked { get; private set; }

        public DisableEntryDeepLinkResult? Presented { get; private set; }

        public bool Confirm(DisableEntryDeepLinkResult result)
        {
            TimesAsked++;
            Presented = result;
            return _answer;
        }
    }

    private static DisableEntryDeepLinkResult Run(
        IReadOnlyList<CategoryGroup> current,
        PendingChangeSet pendingChanges,
        ConfirmationStub confirm,
        params string[] args)
        => new DisableEntryCliCommand().Execute(args, current, pendingChanges, confirm.Confirm);

    [Fact]
    public void Records_the_confirmed_disable_as_a_single_pending_update()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
    }

    [Fact]
    public void Disables_the_matched_entry_so_it_stops_resolving()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        var update = SingleUpdate(pendingChanges);
        update.Target.ShouldBe(target);
        update.Replacement.Status.ShouldBe(HostEntryStatus.Disabled);
    }

    [Fact]
    public void Preserves_the_entry_content_when_disabling_it()
    {
        var target = new HostEntry("10.0.0.1", "api.test", "local box");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "api.test");

        SingleUpdate(pendingChanges).Replacement
            .ShouldBe(new HostEntry("10.0.0.1", "api.test", "local box", HostEntryStatus.Disabled));
    }

    [Fact]
    public void Asks_for_confirmation_before_staging_the_disable()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var confirm = new ConfirmationStub(true);

        Run(current, new PendingChangeSet(), confirm, "--hostname", "web.test");

        confirm.TimesAsked.ShouldBe(1);
    }

    [Fact]
    public void Presents_the_resolved_disable_to_the_confirmation()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var confirm = new ConfirmationStub(true);

        Run(current, new PendingChangeSet(), confirm, "--hostname", "web.test");

        confirm.Presented!.Target.ShouldBe(target);
    }

    [Fact]
    public void Stages_nothing_when_the_confirmation_is_declined()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(false), "--hostname", "web.test");

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Still_asks_for_confirmation_even_when_it_is_declined()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var confirm = new ConfirmationStub(false);

        Run(current, new PendingChangeSet(), confirm, "--hostname", "web.test");

        confirm.TimesAsked.ShouldBe(1);
    }

    [Fact]
    public void Reports_the_resolved_disable_on_the_result()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));

        var result = Run(current, new PendingChangeSet(), new ConfirmationStub(true),
            "--hostname", "web.test");

        result.RequiresConfirmation.ShouldBeTrue();
        result.Target.ShouldBe(target);
        result.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Stops_the_entry_resolving_when_the_confirmed_disable_is_previewed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries.ShouldHaveSingleItem()
            .Status.ShouldBe(HostEntryStatus.Disabled);
    }

    [Fact]
    public void Disables_the_first_active_matching_entry_in_file_order()
    {
        var first = new HostEntry("127.0.0.1", "web.test");
        var second = new HostEntry("10.0.0.1", "web.test");
        var current = Grouped(Group("Work", first), Group("Home", second));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(first);
    }

    [Fact]
    public void Skips_an_already_disabled_entry_to_disable_a_later_active_match()
    {
        var disabled = Disabled("127.0.0.1", "web.test");
        var active = new HostEntry("10.0.0.9", "web.test");
        var current = Grouped(Group(null, disabled, active));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(active);
    }

    [Fact]
    public void Matches_the_hostname_case_insensitively()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "WEB.TEST");

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Matches_option_names_case_insensitively()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--HostName", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Ignores_options_it_does_not_recognise()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--verbose", "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Ignores_a_stray_token_that_is_not_an_option()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "x", "--hostname", "web.test");

        SingleUpdate(pendingChanges).Target.ShouldBe(target);
    }

    [Fact]
    public void Does_not_prompt_and_stages_nothing_for_a_command_with_no_options()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        var result = Run(current, pendingChanges, confirm);

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldNotBeEmpty();
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_prompt_and_stages_nothing_when_the_hostname_is_blank()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        // The trailing --hostname ends the arguments, so it is blank and names no entry.
        var result = Run(current, pendingChanges, confirm, "--hostname");

        result.RequiresConfirmation.ShouldBeFalse();
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Treats_a_hostname_immediately_followed_by_another_option_as_blank()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        // The --hostname value is another option, so it is blank and names no entry.
        var result = Run(current, pendingChanges, confirm, "--hostname", "--verbose");

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("no hostname");
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_prompt_and_stages_nothing_when_the_hostname_matches_no_entry()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        var result = Run(current, pendingChanges, confirm, "--hostname", "absent.test");

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("absent.test");
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_prompt_and_stages_nothing_when_the_only_match_is_already_disabled()
    {
        var current = Grouped(Group(null, Disabled("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        var result = Run(current, pendingChanges, confirm, "--hostname", "web.test");

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("already disabled");
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Should.Throw<ArgumentNullException>(() => new DisableEntryCliCommand().Execute(
            null!,
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            new PendingChangeSet(),
            _ => true));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new DisableEntryCliCommand().Execute(
            new[] { "--hostname", "web.test" },
            null!,
            new PendingChangeSet(),
            _ => true));
    }

    [Fact]
    public void Rejects_a_null_pending_change_set()
    {
        // The hostname matches no entry, so nothing would be recorded — yet a null pending set is
        // still rejected eagerly, before the disable is even resolved.
        Should.Throw<ArgumentNullException>(() => new DisableEntryCliCommand().Execute(
            new[] { "--hostname", "absent.test" },
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            null!,
            _ => true));
    }

    [Fact]
    public void Rejects_a_null_confirmation()
    {
        Should.Throw<ArgumentNullException>(() => new DisableEntryCliCommand().Execute(
            new[] { "--hostname", "web.test" },
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            new PendingChangeSet(),
            null!));
    }
}
