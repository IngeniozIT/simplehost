using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Domain.Tests.Selection;

public class EntrySelectionTests
{
    [Fact]
    public void An_empty_selection_holds_no_entries()
    {
        var selection = EntrySelection.Empty;

        selection.IsEmpty.ShouldBeTrue();
        selection.Count.ShouldBe(0);
        selection.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Selecting_an_entry_adds_it_to_the_selection()
    {
        var entry = new HostEntry("127.0.0.1", "api.test");

        var selection = EntrySelection.Empty.Select(entry);

        selection.IsEmpty.ShouldBeFalse();
        selection.Count.ShouldBe(1);
        selection.Entries.ShouldBe(new[] { entry });
        selection.Contains(entry).ShouldBeTrue();
    }

    [Fact]
    public void Selecting_several_entries_keeps_them_in_selection_order()
    {
        var first = new HostEntry("127.0.0.1", "api.test");
        var second = new HostEntry("127.0.0.2", "web.test");

        var selection = EntrySelection.Empty.Select(first).Select(second);

        selection.Count.ShouldBe(2);
        selection.Entries.ShouldBe(new[] { first, second });
    }

    [Fact]
    public void Selecting_the_same_entry_twice_does_not_duplicate_it()
    {
        var entry = new HostEntry("127.0.0.1", "api.test");

        var selection = EntrySelection.Empty.Select(entry).Select(entry);

        selection.Count.ShouldBe(1);
        selection.Entries.ShouldBe(new[] { entry });
    }

    [Fact]
    public void An_entry_is_identified_by_its_ip_and_hostname_ignoring_comment_and_status()
    {
        var active = new HostEntry("127.0.0.1", "api.test", "note", HostEntryStatus.Active);
        var sameIdentity = new HostEntry("127.0.0.1", "api.test", "different", HostEntryStatus.Disabled);

        var selection = EntrySelection.Empty.Select(active).Select(sameIdentity);

        selection.Count.ShouldBe(1);
        selection.Contains(sameIdentity).ShouldBeTrue();
        selection.Entries.ShouldBe(new[] { active });
    }

    [Fact]
    public void An_entry_is_matched_case_insensitively_by_hostname()
    {
        var lower = new HostEntry("127.0.0.1", "api.test");
        var upper = new HostEntry("127.0.0.1", "API.TEST");

        EntrySelection.Empty.Select(lower).Contains(upper).ShouldBeTrue();
    }

    [Fact]
    public void An_entry_is_matched_exactly_by_ip_address()
    {
        var selection = EntrySelection.Empty.Select(new HostEntry("127.0.0.1", "api.test"));

        selection.Contains(new HostEntry("127.0.0.2", "api.test")).ShouldBeFalse();
    }

    [Fact]
    public void A_differing_hostname_is_not_part_of_the_selection()
    {
        var selection = EntrySelection.Empty.Select(new HostEntry("127.0.0.1", "api.test"));

        selection.Contains(new HostEntry("127.0.0.1", "web.test")).ShouldBeFalse();
    }

    [Fact]
    public void An_entry_absent_from_an_empty_selection_is_not_contained()
    {
        EntrySelection.Empty.Contains(new HostEntry("127.0.0.1", "api.test")).ShouldBeFalse();
    }

    [Fact]
    public void Deselecting_an_entry_removes_it_from_the_selection()
    {
        var first = new HostEntry("127.0.0.1", "api.test");
        var second = new HostEntry("127.0.0.2", "web.test");
        var selection = EntrySelection.Empty.Select(first).Select(second);

        var result = selection.Deselect(first);

        result.Count.ShouldBe(1);
        result.Contains(first).ShouldBeFalse();
        result.Entries.ShouldBe(new[] { second });
    }

    [Fact]
    public void Deselecting_matches_by_identity_ignoring_comment_and_status()
    {
        var selected = new HostEntry("127.0.0.1", "api.test", "note", HostEntryStatus.Active);
        var sameIdentity = new HostEntry("127.0.0.1", "api.test", null, HostEntryStatus.Disabled);
        var selection = EntrySelection.Empty.Select(selected);

        selection.Deselect(sameIdentity).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Deselecting_an_entry_that_is_not_selected_leaves_the_selection_unchanged()
    {
        var selected = new HostEntry("127.0.0.1", "api.test");
        var selection = EntrySelection.Empty.Select(selected);

        var result = selection.Deselect(new HostEntry("127.0.0.9", "other.test"));

        result.ShouldBeSameAs(selection);
        result.Count.ShouldBe(1);
        result.Entries.ShouldBe(new[] { selected });
    }

    [Fact]
    public void Toggling_an_unselected_entry_selects_it()
    {
        var entry = new HostEntry("127.0.0.1", "api.test");

        var selection = EntrySelection.Empty.Toggle(entry);

        selection.Contains(entry).ShouldBeTrue();
        selection.Count.ShouldBe(1);
    }

    [Fact]
    public void Toggling_a_selected_entry_deselects_it()
    {
        var entry = new HostEntry("127.0.0.1", "api.test");
        var selection = EntrySelection.Empty.Select(entry);

        var result = selection.Toggle(entry);

        result.Contains(entry).ShouldBeFalse();
        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Clearing_a_selection_leaves_it_empty()
    {
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "api.test"))
            .Select(new HostEntry("127.0.0.2", "web.test"));

        selection.Clear().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Selecting_does_not_mutate_the_original_selection()
    {
        var original = EntrySelection.Empty;

        original.Select(new HostEntry("127.0.0.1", "api.test"));

        original.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Deselecting_does_not_mutate_the_original_selection()
    {
        var entry = new HostEntry("127.0.0.1", "api.test");
        var original = EntrySelection.Empty.Select(entry);

        original.Deselect(entry);

        original.Count.ShouldBe(1);
        original.Entries.ShouldBe(new[] { entry });
    }
}
