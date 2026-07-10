using Shouldly;
using SimpleHost.Application.Cli;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DeleteEntryCliCommandTests
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

        public DeleteEntryDeepLinkResult? Presented { get; private set; }

        public bool Confirm(DeleteEntryDeepLinkResult result)
        {
            TimesAsked++;
            Presented = result;
            return _answer;
        }
    }

    private static DeleteEntryDeepLinkResult Run(
        IReadOnlyList<CategoryGroup> current,
        PendingChangeSet pendingChanges,
        ConfirmationStub confirm,
        params string[] args)
        => new DeleteEntryCliCommand().Execute(args, current, pendingChanges, confirm.Confirm);

    [Fact]
    public void Records_the_confirmed_deletion_as_a_pending_change()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        SingleChange(pendingChanges).ShouldBe(new DeleteEntryChange(target));
    }

    [Fact]
    public void Asks_for_confirmation_before_staging_the_destructive_deletion()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var confirm = new ConfirmationStub(true);

        Run(current, new PendingChangeSet(), confirm, "--hostname", "web.test");

        confirm.TimesAsked.ShouldBe(1);
    }

    [Fact]
    public void Presents_the_resolved_deletion_to_the_confirmation()
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
    public void Reports_the_resolved_deletion_on_the_result()
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
    public void Removes_the_entry_when_the_confirmed_deletion_is_previewed()
    {
        var current = Grouped(Group(null, new HostEntry("127.0.0.1", "web.test")));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        var projected = pendingChanges.ApplyTo(current);
        projected.Single(group => group.Name is null).Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Deletes_the_first_matching_entry_in_file_order()
    {
        var first = new HostEntry("127.0.0.1", "web.test");
        var second = new HostEntry("10.0.0.1", "web.test");
        var current = Grouped(Group("Work", first), Group("Home", second));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "web.test");

        SingleChange(pendingChanges).ShouldBe(new DeleteEntryChange(first));
    }

    [Fact]
    public void Matches_the_hostname_case_insensitively()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--hostname", "WEB.TEST");

        SingleChange(pendingChanges).ShouldBe(new DeleteEntryChange(target));
    }

    [Fact]
    public void Matches_option_names_case_insensitively()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "--HostName", "web.test");

        SingleChange(pendingChanges).ShouldBe(new DeleteEntryChange(target));
    }

    [Fact]
    public void Ignores_options_it_does_not_recognise()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true),
            "--verbose", "--hostname", "web.test");

        SingleChange(pendingChanges).ShouldBe(new DeleteEntryChange(target));
    }

    [Fact]
    public void Ignores_a_stray_token_that_is_not_an_option()
    {
        var target = new HostEntry("127.0.0.1", "web.test");
        var current = Grouped(Group(null, target));
        var pendingChanges = new PendingChangeSet();

        Run(current, pendingChanges, new ConfirmationStub(true), "x", "--hostname", "web.test");

        SingleChange(pendingChanges).ShouldBe(new DeleteEntryChange(target));
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
    public void Rejects_null_arguments()
    {
        Should.Throw<ArgumentNullException>(() => new DeleteEntryCliCommand().Execute(
            null!,
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            new PendingChangeSet(),
            _ => true));
    }

    [Fact]
    public void Rejects_null_current_entries()
    {
        Should.Throw<ArgumentNullException>(() => new DeleteEntryCliCommand().Execute(
            new[] { "--hostname", "web.test" },
            null!,
            new PendingChangeSet(),
            _ => true));
    }

    [Fact]
    public void Rejects_a_null_pending_change_set()
    {
        Should.Throw<ArgumentNullException>(() => new DeleteEntryCliCommand().Execute(
            new[] { "--hostname", "web.test" },
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            null!,
            _ => true));
    }

    [Fact]
    public void Rejects_a_null_confirmation()
    {
        Should.Throw<ArgumentNullException>(() => new DeleteEntryCliCommand().Execute(
            new[] { "--hostname", "web.test" },
            Grouped(Group(null, new HostEntry("127.0.0.1", "web.test"))),
            new PendingChangeSet(),
            null!));
    }
}
