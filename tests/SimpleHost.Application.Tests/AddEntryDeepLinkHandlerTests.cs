using Shouldly;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class AddEntryDeepLinkHandlerTests
{
    private static DeepLink Link(string link)
    {
        DeepLink.TryParse(link, out var parsed).ShouldBeTrue();
        return parsed!;
    }

    private static AddEntryChange SingleAddChange(PendingChangeSet pendingChanges)
        => pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();

    [Fact]
    public void Records_the_add_link_as_a_pending_change()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), pendingChanges);

        pendingChanges.HasChanges.ShouldBeTrue();
        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
    }

    [Fact]
    public void Records_the_described_entry_with_its_ip_address_and_hostname()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=10.0.0.1&hostname=api.test"), pendingChanges);

        SingleAddChange(pendingChanges).Entry.ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Creates_the_pending_entry_as_active()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), pendingChanges);

        SingleAddChange(pendingChanges).Entry.Status.ShouldBe(HostEntryStatus.Active);
    }

    [Fact]
    public void Places_the_pending_entry_under_the_given_category()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=10.0.0.1&hostname=api.test&category=Work"), pendingChanges);

        SingleAddChange(pendingChanges).Category.ShouldBe("Work");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_given_category()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=10.0.0.1&hostname=api.test&category=%20Work%20"), pendingChanges);

        SingleAddChange(pendingChanges).Category.ShouldBe("Work");
    }

    [Fact]
    public void Leaves_the_pending_entry_uncategorized_when_no_category_is_given()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), pendingChanges);

        SingleAddChange(pendingChanges).Category.ShouldBeNull();
    }

    [Fact]
    public void Treats_a_blank_category_as_no_category()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test&category=%20%20"), pendingChanges);

        SingleAddChange(pendingChanges).Category.ShouldBeNull();
    }

    [Fact]
    public void Attaches_the_comment_the_link_carries()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=0.0.0.0&hostname=ads.example.com&comment=blocked%20ad%20server"),
            pendingChanges);

        SingleAddChange(pendingChanges).Entry.Comment.ShouldBe("blocked ad server");
    }

    [Fact]
    public void Leaves_the_pending_entry_without_a_comment_when_none_is_given()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test"), pendingChanges);

        SingleAddChange(pendingChanges).Entry.Comment.ShouldBeNull();
    }

    [Fact]
    public void Treats_a_blank_comment_as_no_comment()
    {
        var pendingChanges = new PendingChangeSet();

        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=web.test&comment=%20%20"), pendingChanges);

        SingleAddChange(pendingChanges).Entry.Comment.ShouldBeNull();
    }

    [Fact]
    public void Reports_the_created_entry_on_the_result()
    {
        var pendingChanges = new PendingChangeSet();

        var result = new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=0.0.0.0&hostname=ads.example.com&comment=blocked"), pendingChanges);

        result.IsAdded.ShouldBeTrue();
        result.Entry.ShouldBe(new HostEntry("0.0.0.0", "ads.example.com", "blocked"));
        result.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Surfaces_the_added_entry_when_the_pending_changes_are_previewed()
    {
        var pendingChanges = new PendingChangeSet();
        new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=10.0.0.1&hostname=api.test&category=Work"), pendingChanges);

        var projected = pendingChanges.ApplyTo(new[] { new CategoryGroup("Work", Array.Empty<HostEntry>()) });

        projected.Single(group => group.Name == "Work").Entries
            .ShouldHaveSingleItem().ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Rejects_a_link_whose_ip_address_is_missing()
    {
        var pendingChanges = new PendingChangeSet();

        var result = new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?hostname=web.test"), pendingChanges);

        result.IsAdded.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        result.Message.ShouldNotBeEmpty();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_link_whose_ip_address_is_malformed()
    {
        var pendingChanges = new PendingChangeSet();

        var result = new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=1.2.3&hostname=web.test"), pendingChanges);

        result.IsAdded.ShouldBeFalse();
        result.Message.ShouldContain("1.2.3");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_is_missing()
    {
        var pendingChanges = new PendingChangeSet();

        var result = new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1"), pendingChanges);

        result.IsAdded.ShouldBeFalse();
        result.Entry.ShouldBeNull();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_link_whose_hostname_is_malformed()
    {
        var pendingChanges = new PendingChangeSet();

        var result = new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://add?ip=127.0.0.1&hostname=not_a_host"), pendingChanges);

        result.IsAdded.ShouldBeFalse();
        result.Message.ShouldContain("not_a_host");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Refuses_to_handle_a_link_that_is_not_an_add_link()
    {
        var pendingChanges = new PendingChangeSet();

        Should.Throw<ArgumentException>(() => new AddEntryDeepLinkHandler().Handle(
            Link("simplehost://update?ip=127.0.0.1&hostname=web.test"), pendingChanges));

        pendingChanges.HasChanges.ShouldBeFalse();
    }
}
