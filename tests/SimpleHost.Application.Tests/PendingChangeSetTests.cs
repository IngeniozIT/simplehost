using Shouldly;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class PendingChangeSetTests
{
    private static IReadOnlyList<CategoryGroup> Grouping(params CategoryGroup[] groups) => groups;

    private static CategoryGroup NoCategory(params HostEntry[] entries) => new(null, entries);

    private static IReadOnlyList<HostEntry> EntriesUnder(IReadOnlyList<CategoryGroup> groups, string? name)
        => groups.Single(group => group.Name == name).Entries;

    [Fact]
    public void Has_no_changes_before_any_edit_is_recorded()
    {
        new PendingChangeSet().HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Reports_changes_once_an_edit_is_recorded()
    {
        var changes = new PendingChangeSet();

        changes.AddEntry(new HostEntry("127.0.0.1", "web.test"));

        changes.HasChanges.ShouldBeTrue();
        changes.Changes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Applying_an_empty_set_returns_the_current_grouping_unchanged()
    {
        var current = Grouping(
            NoCategory(new HostEntry("127.0.0.1", "web.test")),
            new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }));

        var projected = new PendingChangeSet().ApplyTo(current);

        projected.Select(group => group.Name).ShouldBe(new string?[] { null, "Work" });
        projected.Zip(current).ShouldAllBe(pair => pair.First.Entries.SequenceEqual(pair.Second.Entries));
    }

    [Fact]
    public void Does_not_mutate_the_current_grouping_when_projecting()
    {
        var current = Grouping(NoCategory(new HostEntry("127.0.0.1", "web.test")));
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"));

        changes.ApplyTo(current);

        EntriesUnder(current, null).ShouldHaveSingleItem();
    }

    [Fact]
    public void Projects_an_added_entry_as_the_last_entry_of_its_category()
    {
        var current = Grouping(
            NoCategory(),
            new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }));
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.2", "db.test"), "Work");

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, "Work").ShouldBe(new[]
        {
            new HostEntry("10.0.0.1", "api.test"),
            new HostEntry("10.0.0.2", "db.test"),
        });
    }

    [Fact]
    public void Projects_an_added_entry_without_a_category_into_no_category()
    {
        var current = Grouping(NoCategory());
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("127.0.0.1", "web.test"));

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "web.test"));
    }

    [Fact]
    public void Projects_an_updated_entry_in_place()
    {
        var current = Grouping(NoCategory(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("10.0.0.1", "api.test")));
        var changes = new PendingChangeSet();
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("192.168.0.1", "web.test"));

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldBe(new[]
        {
            new HostEntry("192.168.0.1", "web.test"),
            new HostEntry("10.0.0.1", "api.test"),
        });
    }

    [Fact]
    public void Projects_a_deleted_entry_out_of_its_category()
    {
        var current = Grouping(NoCategory(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("10.0.0.1", "api.test")));
        var changes = new PendingChangeSet();
        changes.DeleteEntry(new HostEntry("127.0.0.1", "web.test"));

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Keeps_an_emptied_category_present_after_deleting_its_last_entry()
    {
        var current = Grouping(
            NoCategory(),
            new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }));
        var changes = new PendingChangeSet();
        changes.DeleteEntry(new HostEntry("10.0.0.1", "api.test"));

        var projected = changes.ApplyTo(current);

        projected.Single(group => group.Name == "Work").Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Projects_a_moved_entry_into_its_target_category()
    {
        var current = Grouping(
            NoCategory(new HostEntry("127.0.0.1", "web.test")),
            new CategoryGroup("Work", Array.Empty<HostEntry>()));
        var changes = new PendingChangeSet();
        changes.MoveEntry(new HostEntry("127.0.0.1", "web.test"), "Work");

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldBeEmpty();
        EntriesUnder(projected, "Work").ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "web.test"));
    }

    [Fact]
    public void Projects_a_reordered_entry_to_the_top_of_its_category()
    {
        var current = Grouping(new CategoryGroup("Work", new[]
        {
            new HostEntry("10.0.0.1", "api.test"),
            new HostEntry("10.0.0.2", "db.test"),
            new HostEntry("10.0.0.3", "cache.test"),
        }));
        var changes = new PendingChangeSet();
        changes.ReorderEntry(new HostEntry("10.0.0.3", "cache.test"), 0);

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, "Work").ShouldBe(new[]
        {
            new HostEntry("10.0.0.3", "cache.test"),
            new HostEntry("10.0.0.1", "api.test"),
            new HostEntry("10.0.0.2", "db.test"),
        });
    }

    [Fact]
    public void Projects_a_reordered_entry_to_the_bottom_of_its_category()
    {
        var current = Grouping(new CategoryGroup("Work", new[]
        {
            new HostEntry("10.0.0.1", "api.test"),
            new HostEntry("10.0.0.2", "db.test"),
            new HostEntry("10.0.0.3", "cache.test"),
        }));
        var changes = new PendingChangeSet();
        changes.ReorderEntry(new HostEntry("10.0.0.1", "api.test"), 2);

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, "Work").ShouldBe(new[]
        {
            new HostEntry("10.0.0.2", "db.test"),
            new HostEntry("10.0.0.3", "cache.test"),
            new HostEntry("10.0.0.1", "api.test"),
        });
    }

    [Fact]
    public void Leaves_other_categories_untouched_when_reordering_within_one()
    {
        var current = Grouping(
            NoCategory(new HostEntry("127.0.0.1", "web.test")),
            new CategoryGroup("Work", new[]
            {
                new HostEntry("10.0.0.1", "api.test"),
                new HostEntry("10.0.0.2", "db.test"),
            }));
        var changes = new PendingChangeSet();
        changes.ReorderEntry(new HostEntry("10.0.0.2", "db.test"), 0);

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "web.test"));
        EntriesUnder(projected, "Work").ShouldBe(new[]
        {
            new HostEntry("10.0.0.2", "db.test"),
            new HostEntry("10.0.0.1", "api.test"),
        });
    }

    [Fact]
    public void Throws_when_reordering_an_entry_that_is_not_present()
    {
        var changes = new PendingChangeSet();
        changes.ReorderEntry(new HostEntry("127.0.0.1", "web.test"), 0);

        Should.Throw<InvalidOperationException>(() => changes.ApplyTo(Grouping(NoCategory())))
            .Message.ShouldContain("No entry found matching");
    }

    [Fact]
    public void Applies_accumulated_changes_in_the_order_they_were_recorded()
    {
        var current = Grouping(NoCategory());
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("127.0.0.1", "web.test"));
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("192.168.0.1", "web.test"));

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("192.168.0.1", "web.test"));
    }

    [Fact]
    public void Projects_a_created_category_as_a_new_empty_group_after_the_existing_groups()
    {
        var current = Grouping(
            NoCategory(new HostEntry("127.0.0.1", "web.test")),
            new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }));
        var changes = new PendingChangeSet();
        changes.CreateCategory("Home");

        var projected = changes.ApplyTo(current);

        projected.Select(group => group.Name).ShouldBe(new string?[] { null, "Work", "Home" });
        EntriesUnder(projected, "Home").ShouldBeEmpty();
    }

    [Fact]
    public void Records_a_category_creation_as_a_pending_change()
    {
        var changes = new PendingChangeSet();

        changes.CreateCategory("Work");

        changes.HasChanges.ShouldBeTrue();
        changes.Changes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Projects_a_created_category_under_its_trimmed_name()
    {
        var changes = new PendingChangeSet();
        changes.CreateCategory("  Home  ");

        var projected = changes.ApplyTo(Grouping(NoCategory()));

        projected.Select(group => group.Name).ShouldBe(new string?[] { null, "Home" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_creating_a_category_with_an_empty_or_whitespace_name(string name)
    {
        var changes = new PendingChangeSet();

        Should.Throw<ArgumentException>(() => changes.CreateCategory(name));
        changes.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Throws_when_adding_under_a_category_that_has_no_marker()
    {
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"), "Missing");

        Should.Throw<InvalidOperationException>(() => changes.ApplyTo(Grouping(NoCategory())))
            .Message.ShouldContain("No category marker found for 'Missing'.");
    }

    [Fact]
    public void Throws_when_updating_an_entry_that_is_not_present()
    {
        var changes = new PendingChangeSet();
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("192.168.0.1", "web.test"));

        Should.Throw<InvalidOperationException>(() => changes.ApplyTo(Grouping(NoCategory())))
            .Message.ShouldContain("No entry found matching");
    }
}
