using Shouldly;
using SimpleHost.Application.Cli;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class UpdateEntryCliCommandTests
{
    private static IReadOnlyList<CategoryGroup> Grouped(params CategoryGroup[] groups) => groups;

    private static CategoryGroup Group(string? name, params HostEntry[] entries) => new(name, entries);

    private static PendingChange SingleChange(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem();

    /// <summary>A confirmation stub that records whether it was asked and answers a fixed way.</summary>
    private sealed class ConfirmationStub
    {
        private readonly bool _answer;

        public ConfirmationStub(bool answer) => _answer = answer;

        public int TimesAsked { get; private set; }

        public UpdateEntryDeepLinkResult? Presented { get; private set; }

        public bool Confirm(UpdateEntryDeepLinkResult result)
        {
            TimesAsked++;
            Presented = result;
            return _answer;
        }
    }

    private static UpdateEntryDeepLinkResult Run(
        IReadOnlyList<CategoryGroup> current,
        PendingChangeSet pendingChanges,
        ConfirmationStub confirm,
        params string[] args)
        => new UpdateEntryCliCommand().Execute(args, current, pendingChanges, confirm.Confirm);

    [Fact]
    public void Records_the_confirmed_ip_update_as_a_pending_change()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--hostname", "web.test", "--ip", "10.0.0.1");

        SingleChange(pendingChanges)
            .ShouldBe(new UpdateEntryChange(target, new HostEntry("10.0.0.1", "web.test")));
    }

    [Fact]
    public void Records_the_confirmed_category_change_as_a_pending_move()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group("Work", target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--hostname", "web.test", "--category", "Home");

        SingleChange(pendingChanges).ShouldBe(new MoveEntryChange(target, "Home"));
    }

    [Fact]
    public void Records_both_the_ip_update_and_the_move_when_both_are_confirmed()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group("Work", target), Group("Home"));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--hostname", "web.test", "--ip", "10.0.0.1", "--category", "Home");

        var replacement = new HostEntry("10.0.0.1", "web.test");
        pendingChanges.Changes.Count.ShouldBe(2);
        pendingChanges.Changes[0].ShouldBe(new UpdateEntryChange(target, replacement));
        pendingChanges.Changes[1].ShouldBe(new MoveEntryChange(replacement, "Home"));
    }

    [Fact]
    public void Asks_for_confirmation_before_staging_the_destructive_change()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var confirm = new ConfirmationStub(true);

        Run(current, new PendingChangeSet(), confirm, "--hostname", "web.test", "--ip", "10.0.0.1");

        confirm.TimesAsked.ShouldBe(1);
    }

    [Fact]
    public void Presents_the_resolved_change_to_the_confirmation()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var confirm = new ConfirmationStub(true);

        Run(current, new PendingChangeSet(), confirm, "--hostname", "web.test", "--ip", "10.0.0.1");

        confirm.Presented!.Target.ShouldBe(target);
        confirm.Presented!.Replacement.ShouldBe(new HostEntry("10.0.0.1", "web.test"));
    }

    [Fact]
    public void Stages_nothing_when_the_confirmation_is_declined()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(false),
            "--hostname", "web.test", "--ip", "10.0.0.1");

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Still_asks_for_confirmation_even_when_it_is_declined()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var confirm = new ConfirmationStub(false);

        Run(current, new PendingChangeSet(), confirm, "--hostname", "web.test", "--ip", "10.0.0.1");

        confirm.TimesAsked.ShouldBe(1);
    }

    [Fact]
    public void Reports_the_resolved_update_on_the_result()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));

        var result = Run(current, new PendingChangeSet(), new ConfirmationStub(true),
            "--hostname", "web.test", "--ip", "10.0.0.1");

        result.RequiresConfirmation.ShouldBeTrue();
        result.Target.ShouldBe(target);
        result.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Surfaces_the_updated_ip_when_the_confirmed_changes_are_previewed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--hostname", "web.test", "--ip", "10.0.0.1");

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries
            .ShouldHaveSingleItem().ShouldBe(new HostEntry("10.0.0.1", "web.test"));
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_given_category()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group("Work", target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--hostname", "web.test", "--category", " Home ");

        SingleChange(pendingChanges).ShouldBe(new MoveEntryChange(target, "Home"));
    }

    [Fact]
    public void Matches_option_names_case_insensitively()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--HostName", "web.test", "--IP", "10.0.0.1");

        SingleChange(pendingChanges)
            .ShouldBe(new UpdateEntryChange(target, new HostEntry("10.0.0.1", "web.test")));
    }

    [Fact]
    public void Ignores_options_it_does_not_recognise()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--verbose", "--hostname", "web.test", "--ip", "10.0.0.1");

        SingleChange(pendingChanges)
            .ShouldBe(new UpdateEntryChange(target, new HostEntry("10.0.0.1", "web.test")));
    }

    [Fact]
    public void Ignores_a_stray_token_that_is_not_an_option()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "x", "--hostname", "web.test", "--ip", "10.0.0.1");

        SingleChange(pendingChanges)
            .ShouldBe(new UpdateEntryChange(target, new HostEntry("10.0.0.1", "web.test")));
    }

    [Fact]
    public void Treats_an_option_immediately_followed_by_another_option_as_blank()
    {
        var current = Grouped(Group("Work", new HostEntry("127.0.0.1", "web.test")));
        var confirm = new ConfirmationStub(true);

        var result = Run(current, new PendingChangeSet(), confirm,
            "--ip", "--hostname", "web.test", "--category", "Home");

        // The blank --ip requests no IP change; only the category move remains.
        result.ChangesIpAddress.ShouldBeFalse();
        result.ChangesCategory.ShouldBeTrue();
    }

    [Fact]
    public void Treats_a_trailing_option_with_no_value_as_blank()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--hostname", "web.test", "--ip", "10.0.0.1", "--category");

        // The trailing --category ends the arguments, so it is blank and requests no category
        // change; only the IP update is staged.
        SingleChange(pendingChanges)
            .ShouldBe(new UpdateEntryChange(target, new HostEntry("10.0.0.1", "web.test")));
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
    public void Does_not_prompt_and_stages_nothing_when_the_hostname_is_missing()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        var result = Run(current, pendingChanges, confirm, "--ip", "10.0.0.1");

        result.RequiresConfirmation.ShouldBeFalse();
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_prompt_and_stages_nothing_when_the_hostname_matches_no_entry()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        var result = Run(current, pendingChanges, confirm, "--hostname", "absent.test", "--ip", "10.0.0.1");

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("absent.test");
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_prompt_and_stages_nothing_when_the_ip_is_malformed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        var result = Run(current, pendingChanges, confirm, "--hostname", "web.test", "--ip", "1.2.3");

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("1.2.3");
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_prompt_and_stages_nothing_when_the_command_requests_no_change()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();
        var confirm = new ConfirmationStub(true);

        var result = Run(current, pendingChanges, confirm, "--hostname", "web.test");

        result.RequiresConfirmation.ShouldBeFalse();
        result.Message.ShouldContain("no change");
        confirm.TimesAsked.ShouldBe(0);
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Should.Throw<ArgumentNullException>(() => new UpdateEntryCliCommand().Execute(
            null!,
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            new PendingChangeSet(),
            _ => true));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new UpdateEntryCliCommand().Execute(
            new[] { "--hostname", "web.test", "--ip", "10.0.0.1" },
            null!,
            new PendingChangeSet(),
            _ => true));
    }

    [Fact]
    public void Rejects_a_null_pending_change_set()
    {
        Should.Throw<ArgumentNullException>(() => new UpdateEntryCliCommand().Execute(
            new[] { "--hostname", "web.test", "--ip", "10.0.0.1" },
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            null!,
            _ => true));
    }

    [Fact]
    public void Rejects_a_null_confirmation()
    {
        Should.Throw<ArgumentNullException>(() => new UpdateEntryCliCommand().Execute(
            new[] { "--hostname", "web.test", "--ip", "10.0.0.1" },
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            new PendingChangeSet(),
            null!));
    }
}
