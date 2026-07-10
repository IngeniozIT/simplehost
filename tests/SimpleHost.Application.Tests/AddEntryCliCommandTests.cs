using Shouldly;
using SimpleHost.Application.Cli;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class AddEntryCliCommandTests
{
    private static AddEntryDeepLinkResult Run(PendingChangeSet pendingChanges, params string[] args)
        => new AddEntryCliCommand().Execute(args, pendingChanges);

    private static AddEntryChange SingleAddChange(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();

    [Fact]
    public void Records_the_add_command_as_a_pending_change()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "127.0.0.1", "--hostname", "web.test");

        pendingChanges.HasChanges.ShouldBeTrue();
        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
    }

    [Fact]
    public void Records_the_described_entry_with_its_ip_address_and_hostname()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "10.0.0.1", "--hostname", "api.test");

        SingleAddChange(pendingChanges).Entry.ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Creates_the_pending_entry_as_active()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "127.0.0.1", "--hostname", "web.test");

        SingleAddChange(pendingChanges).Entry.Status.ShouldBe(HostEntryStatus.Active);
    }

    [Fact]
    public void Places_the_pending_entry_under_the_given_category()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "10.0.0.1", "--hostname", "api.test", "--category", "Work");

        SingleAddChange(pendingChanges).Category.ShouldBe("Work");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_given_category()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "10.0.0.1", "--hostname", "api.test", "--category", " Work ");

        SingleAddChange(pendingChanges).Category.ShouldBe("Work");
    }

    [Fact]
    public void Leaves_the_pending_entry_uncategorized_when_no_category_is_given()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "127.0.0.1", "--hostname", "web.test");

        SingleAddChange(pendingChanges).Category.ShouldBeNull();
    }

    [Fact]
    public void Treats_a_blank_category_as_no_category()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "127.0.0.1", "--hostname", "web.test", "--category", "   ");

        SingleAddChange(pendingChanges).Category.ShouldBeNull();
    }

    [Fact]
    public void Attaches_the_comment_the_command_carries()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "0.0.0.0", "--hostname", "ads.example.com",
            "--comment", "blocked ad server");

        SingleAddChange(pendingChanges).Entry.Comment.ShouldBe("blocked ad server");
    }

    [Fact]
    public void Leaves_the_pending_entry_without_a_comment_when_none_is_given()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "127.0.0.1", "--hostname", "web.test");

        SingleAddChange(pendingChanges).Entry.Comment.ShouldBeNull();
    }

    [Fact]
    public void Treats_a_blank_comment_as_no_comment()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--ip", "127.0.0.1", "--hostname", "web.test", "--comment", "   ");

        SingleAddChange(pendingChanges).Entry.Comment.ShouldBeNull();
    }

    [Fact]
    public void Reports_the_created_entry_on_the_result()
    {
        var pendingChanges = new PendingChangeSet();

        var result = Run(pendingChanges, "--ip", "0.0.0.0", "--hostname", "ads.example.com",
            "--comment", "blocked");

        result.IsAdded.ShouldBeTrue();
        result.Entry.ShouldBe(new HostEntry("0.0.0.0", "ads.example.com", "blocked"));
        result.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Surfaces_the_added_entry_when_the_pending_changes_are_previewed()
    {
        var pendingChanges = new PendingChangeSet();
        Run(pendingChanges, "--ip", "10.0.0.1", "--hostname", "api.test", "--category", "Work");

        var projected = pendingChanges.ApplyTo(new[] { new CategoryGroup("Work", Array.Empty<HostEntry>()) });

        projected.Single(group => group.Name == "Work").Entries
            .ShouldHaveSingleItem().ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Matches_option_names_case_insensitively()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--IP", "10.0.0.1", "--HostName", "api.test");

        SingleAddChange(pendingChanges).Entry.ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Ignores_options_it_does_not_recognise()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--verbose", "--ip", "10.0.0.1", "--hostname", "api.test");

        SingleAddChange(pendingChanges).Entry.ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Ignores_a_stray_token_that_is_not_an_option()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "x", "--ip", "10.0.0.1", "--hostname", "api.test");

        SingleAddChange(pendingChanges).Entry.ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Treats_an_option_immediately_followed_by_another_option_as_blank()
    {
        var pendingChanges = new PendingChangeSet();

        Run(pendingChanges, "--category", "--ip", "10.0.0.1", "--hostname", "api.test");

        SingleAddChange(pendingChanges).Category.ShouldBeNull();
    }

    [Fact]
    public void Treats_a_trailing_option_with_no_value_as_blank()
    {
        var pendingChanges = new PendingChangeSet();

        var result = Run(pendingChanges, "--ip", "127.0.0.1", "--hostname");

        result.IsAdded.ShouldBeFalse();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_command_with_no_options_at_all()
    {
        var pendingChanges = new PendingChangeSet();

        var result = Run(pendingChanges);

        result.IsAdded.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        result.Message.ShouldNotBeEmpty();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_command_whose_ip_address_is_missing()
    {
        var pendingChanges = new PendingChangeSet();

        var result = Run(pendingChanges, "--hostname", "web.test");

        result.IsAdded.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        result.Message.ShouldNotBeEmpty();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_command_whose_ip_address_is_malformed()
    {
        var pendingChanges = new PendingChangeSet();

        var result = Run(pendingChanges, "--ip", "1.2.3", "--hostname", "web.test");

        result.IsAdded.ShouldBeFalse();
        result.Message.ShouldContain("1.2.3");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_command_whose_hostname_is_missing()
    {
        var pendingChanges = new PendingChangeSet();

        var result = Run(pendingChanges, "--ip", "127.0.0.1");

        result.IsAdded.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_command_whose_hostname_is_malformed()
    {
        var pendingChanges = new PendingChangeSet();

        var result = Run(pendingChanges, "--ip", "127.0.0.1", "--hostname", "not_a_host");

        result.IsAdded.ShouldBeFalse();
        result.Message.ShouldContain("not_a_host");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Should.Throw<ArgumentNullException>(
            () => new AddEntryCliCommand().Execute(null!, new PendingChangeSet()));
    }

    [Fact]
    public void Rejects_a_null_pending_change_set()
    {
        Should.Throw<ArgumentNullException>(
            () => new AddEntryCliCommand().Execute(new[] { "--ip", "127.0.0.1" }, null!));
    }
}
