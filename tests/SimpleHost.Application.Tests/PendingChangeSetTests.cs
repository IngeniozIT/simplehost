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
    public void Raises_changed_when_a_change_is_recorded()
    {
        var changes = new PendingChangeSet();
        var raised = 0;
        changes.Changed += (_, _) => raised++;

        changes.AddEntry(new HostEntry("127.0.0.1", "web.test"));

        raised.ShouldBe(1);
    }

    [Fact]
    public void Raises_changed_when_cleared()
    {
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("127.0.0.1", "web.test"));
        var raised = 0;
        changes.Changed += (_, _) => raised++;

        changes.Clear();

        raised.ShouldBe(1);
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
    public void Skips_reordering_an_entry_that_is_not_present()
    {
        var changes = new PendingChangeSet();
        changes.ReorderEntry(new HostEntry("127.0.0.1", "web.test"), 0);

        var projected = changes.ApplyTo(Grouping(NoCategory()));

        EntriesUnder(projected, null).ShouldBeEmpty();
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
    public void Skips_adding_under_a_category_that_has_no_marker()
    {
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"), "Missing");

        var projected = changes.ApplyTo(Grouping(NoCategory()));

        projected.Select(group => group.Name).ShouldBe(new string?[] { null });
        EntriesUnder(projected, null).ShouldBeEmpty();
    }

    [Fact]
    public void Skips_updating_an_entry_that_is_not_present()
    {
        var changes = new PendingChangeSet();
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("192.168.0.1", "web.test"));

        var projected = changes.ApplyTo(Grouping(NoCategory()));

        EntriesUnder(projected, null).ShouldBeEmpty();
    }

    [Fact]
    public void Records_a_category_rename_as_a_pending_change()
    {
        var changes = new PendingChangeSet();

        changes.RenameCategory("Work", "Office");

        changes.HasChanges.ShouldBeTrue();
        changes.Changes.ShouldHaveSingleItem().ShouldBeOfType<RenameCategoryChange>();
    }

    [Fact]
    public void Projects_a_renamed_category_keeping_its_entries_in_place()
    {
        var current = Grouping(
            NoCategory(new HostEntry("127.0.0.1", "web.test")),
            new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }));
        var changes = new PendingChangeSet();
        changes.RenameCategory("Work", "Office");

        var projected = changes.ApplyTo(current);

        projected.Select(group => group.Name).ShouldBe(new string?[] { null, "Office" });
        EntriesUnder(projected, "Office").ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("10.0.0.1", "api.test"));
        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "web.test"));
    }

    [Fact]
    public void Projects_a_renamed_category_under_its_trimmed_new_name()
    {
        var changes = new PendingChangeSet();
        changes.RenameCategory("Work", "  Office  ");

        var projected = changes.ApplyTo(Grouping(
            NoCategory(),
            new CategoryGroup("Work", Array.Empty<HostEntry>())));

        projected.Select(group => group.Name).ShouldBe(new string?[] { null, "Office" });
    }

    [Fact]
    public void Skips_renaming_a_category_that_has_no_marker()
    {
        var changes = new PendingChangeSet();
        changes.RenameCategory("Missing", "Office");

        var projected = changes.ApplyTo(Grouping(NoCategory()));

        projected.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_renaming_when_the_current_name_is_empty_or_whitespace(string currentName)
    {
        var changes = new PendingChangeSet();

        Should.Throw<ArgumentException>(() => changes.RenameCategory(currentName, "Office"));
        changes.HasChanges.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_renaming_to_an_empty_or_whitespace_name(string newName)
    {
        var changes = new PendingChangeSet();

        Should.Throw<ArgumentException>(() => changes.RenameCategory("Work", newName));
        changes.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Records_a_category_deletion_as_a_pending_change()
    {
        var changes = new PendingChangeSet();

        changes.DeleteCategory("Work");

        changes.HasChanges.ShouldBeTrue();
        changes.Changes.ShouldHaveSingleItem().ShouldBeOfType<DeleteCategoryChange>();
    }

    [Fact]
    public void Projects_a_deleted_category_reparenting_its_entries_into_no_category()
    {
        var current = Grouping(
            NoCategory(new HostEntry("127.0.0.1", "web.test")),
            new CategoryGroup("Work", new[]
            {
                new HostEntry("10.0.0.1", "api.test"),
                new HostEntry("10.0.0.2", "db.test"),
            }));
        var changes = new PendingChangeSet();
        changes.DeleteCategory("Work");

        var projected = changes.ApplyTo(current);

        projected.Select(group => group.Name).ShouldBe(new string?[] { null });
        EntriesUnder(projected, null).ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("10.0.0.1", "api.test"),
            new HostEntry("10.0.0.2", "db.test"),
        });
    }

    [Fact]
    public void Leaves_other_categories_in_place_when_deleting_one()
    {
        var current = Grouping(
            NoCategory(),
            new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }),
            new CategoryGroup("Home", new[] { new HostEntry("192.168.0.1", "nas.test") }));
        var changes = new PendingChangeSet();
        changes.DeleteCategory("Work");

        var projected = changes.ApplyTo(current);

        projected.Select(group => group.Name).ShouldBe(new string?[] { null, "Home" });
        EntriesUnder(projected, "Home").ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("192.168.0.1", "nas.test"));
        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Skips_deleting_a_category_that_has_no_marker()
    {
        var changes = new PendingChangeSet();
        changes.DeleteCategory("Missing");

        var projected = changes.ApplyTo(Grouping(NoCategory()));

        projected.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    [Fact]
    public void Skips_deleting_a_category_when_there_is_no_no_category_group_to_reparent_into()
    {
        var current = Grouping(new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }));
        var changes = new PendingChangeSet();
        changes.DeleteCategory("Work");

        var projected = changes.ApplyTo(current);

        projected.Select(group => group.Name).ShouldBe(new[] { "Work" });
        EntriesUnder(projected, "Work").ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Skips_a_move_whose_entry_is_not_present()
    {
        var current = Grouping(
            NoCategory(),
            new CategoryGroup("Work", Array.Empty<HostEntry>()));
        var changes = new PendingChangeSet();
        changes.MoveEntry(new HostEntry("127.0.0.1", "web.test"), "Work");

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldBeEmpty();
        EntriesUnder(projected, "Work").ShouldBeEmpty();
    }

    [Fact]
    public void Skips_a_move_whose_target_category_is_missing_without_dropping_the_entry()
    {
        var current = Grouping(NoCategory(new HostEntry("127.0.0.1", "web.test")));
        var changes = new PendingChangeSet();
        changes.MoveEntry(new HostEntry("127.0.0.1", "web.test"), "Missing");

        var projected = changes.ApplyTo(current);

        projected.Select(group => group.Name).ShouldBe(new string?[] { null });
        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "web.test"));
    }

    [Fact]
    public void Skips_a_stale_change_whose_target_an_earlier_change_already_removed()
    {
        var current = Grouping(NoCategory(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("10.0.0.1", "api.test")));
        var changes = new PendingChangeSet();
        changes.DeleteEntry(new HostEntry("127.0.0.1", "web.test"));
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("127.0.0.1", "web.test", null, HostEntryStatus.Disabled));

        var projected = changes.ApplyTo(current);

        EntriesUnder(projected, null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_deleting_a_category_with_an_empty_or_whitespace_name(string name)
    {
        var changes = new PendingChangeSet();

        Should.Throw<ArgumentException>(() => changes.DeleteCategory(name));
        changes.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Inverse_updates_on_the_same_entry_cancel_out()
    {
        var active = new HostEntry("127.0.0.1", "web.test");
        var disabled = active with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        changes.UpdateEntry(active, disabled);
        changes.UpdateEntry(disabled, active);

        changes.HasChanges.ShouldBeFalse();
        changes.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void Repeating_the_same_update_keeps_a_single_pending_change()
    {
        var active = new HostEntry("127.0.0.1", "web.test");
        var disabled = active with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        changes.UpdateEntry(active, disabled);
        changes.UpdateEntry(active, disabled);

        changes.Changes.ShouldHaveSingleItem()
            .ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(disabled);
    }

    [Fact]
    public void Chained_updates_on_the_same_entry_collapse_into_a_single_net_update()
    {
        var current = Grouping(NoCategory(new HostEntry("127.0.0.1", "web.test")));
        var changes = new PendingChangeSet();

        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"), new HostEntry("10.0.0.1", "web.test"));
        changes.UpdateEntry(
            new HostEntry("10.0.0.1", "web.test"), new HostEntry("192.168.0.1", "web.test"));

        var change = changes.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("127.0.0.1", "web.test"));
        change.Replacement.ShouldBe(new HostEntry("192.168.0.1", "web.test"));
        EntriesUnder(changes.ApplyTo(current), null).ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("192.168.0.1", "web.test"));
    }

    [Fact]
    public void A_later_update_from_the_same_original_supersedes_the_earlier_one()
    {
        var original = new HostEntry("127.0.0.1", "web.test");
        var changes = new PendingChangeSet();

        changes.UpdateEntry(original, original with { IpAddress = "10.0.0.1" });
        changes.UpdateEntry(original, original with { IpAddress = "192.168.0.1" });

        changes.Changes.ShouldHaveSingleItem()
            .ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(original with { IpAddress = "192.168.0.1" });
    }

    [Fact]
    public void Raises_changed_on_every_record_even_when_coalescing_cancels_a_change()
    {
        var active = new HostEntry("127.0.0.1", "web.test");
        var disabled = active with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();
        var raised = 0;
        changes.Changed += (_, _) => raised++;

        changes.UpdateEntry(active, disabled);
        changes.UpdateEntry(disabled, active);

        raised.ShouldBe(2);
        changes.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void An_inverse_update_coalesces_across_an_update_on_another_entry()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var api = new HostEntry("10.0.0.1", "api.test");
        var apiDisabled = api with { Status = HostEntryStatus.Disabled };
        var current = Grouping(NoCategory(web, api));
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.UpdateEntry(api, apiDisabled);
        changes.UpdateEntry(webDisabled, web);

        // The intervening change is on another entry, so it is crossed and the inverse pair on web
        // still cancels; only the api change is left, and the net projection is correct.
        changes.Changes.ShouldHaveSingleItem()
            .ShouldBeOfType<UpdateEntryChange>()
            .Target.ShouldBe(api);
        EntriesUnder(changes.ApplyTo(current), null).ShouldBe(new[] { web, apiDisabled });
    }

    [Fact]
    public void A_superseding_update_coalesces_across_an_update_on_another_entry_keeping_its_position()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var api = new HostEntry("10.0.0.1", "api.test");
        var webRenamed = web with { IpAddress = "127.0.0.2" };
        var webRenamedAgain = web with { IpAddress = "127.0.0.3" };
        var apiRenamed = api with { IpAddress = "10.0.0.2" };
        var current = Grouping(NoCategory(web, api));
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webRenamed);
        changes.UpdateEntry(api, apiRenamed);
        changes.UpdateEntry(webRenamed, webRenamedAgain);

        // The web update is superseded in place across the api update, keeping its original target and
        // its leading position; the api update is untouched.
        changes.Changes.Count.ShouldBe(2);
        var first = changes.Changes[0].ShouldBeOfType<UpdateEntryChange>();
        first.Target.ShouldBe(web);
        first.Replacement.ShouldBe(webRenamedAgain);
        changes.Changes[1].ShouldBeOfType<UpdateEntryChange>().Target.ShouldBe(api);
        EntriesUnder(changes.ApplyTo(current), null).ShouldBe(new[] { webRenamedAgain, apiRenamed });
    }

    [Fact]
    public void A_full_enable_disable_enable_cycle_across_a_category_nets_to_no_changes()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var api = new HostEntry("10.0.0.1", "api.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var apiDisabled = api with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        // Disable all: one update per entry, staged interleaved across the entries.
        changes.UpdateEntry(web, webDisabled);
        changes.UpdateEntry(api, apiDisabled);
        // Enable all again: the inverse update per entry, again interleaved.
        changes.UpdateEntry(webDisabled, web);
        changes.UpdateEntry(apiDisabled, api);

        changes.HasChanges.ShouldBeFalse();
        changes.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void Disabling_every_entry_once_stays_one_change_per_entry()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var api = new HostEntry("10.0.0.1", "api.test");
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, web with { Status = HostEntryStatus.Disabled });
        changes.UpdateEntry(api, api with { Status = HostEntryStatus.Disabled });

        // Different entries never falsely coalesce, so one update stays per disabled entry.
        changes.Changes.Count.ShouldBe(2);
    }

    [Fact]
    public void A_same_entry_move_between_two_updates_prevents_coalescing()
    {
        var current = Grouping(
            NoCategory(new HostEntry("127.0.0.1", "web.test")),
            new CategoryGroup("Work", Array.Empty<HostEntry>()));
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.MoveEntry(webDisabled, "Work");
        changes.UpdateEntry(webDisabled, web);

        // The intervening move acts on the same entry, so it breaks the chain and the inverse update is
        // kept rather than reordered across the move; the net projection is still correct.
        changes.Changes.Count.ShouldBe(3);
        EntriesUnder(changes.ApplyTo(current), "Work").ShouldHaveSingleItem().ShouldBe(web);
        EntriesUnder(changes.ApplyTo(current), null).ShouldBeEmpty();
    }

    [Fact]
    public void An_inverse_update_coalesces_across_a_move_on_another_entry()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var api = new HostEntry("10.0.0.1", "api.test");
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.MoveEntry(api, "Work");
        changes.UpdateEntry(webDisabled, web);

        // The move is on another entry, so it is crossed and the web pair cancels; only the move stays.
        changes.Changes.ShouldHaveSingleItem().ShouldBeOfType<MoveEntryChange>()
            .Entry.ShouldBe(api);
    }

    [Fact]
    public void A_same_entry_delete_between_two_updates_prevents_coalescing()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.DeleteEntry(webDisabled);
        changes.UpdateEntry(webDisabled, web);

        // The delete acts on the same entry, so it breaks the chain and the inverse update is kept.
        changes.Changes.Count.ShouldBe(3);
    }

    [Fact]
    public void An_inverse_update_coalesces_across_a_delete_on_another_entry()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var api = new HostEntry("10.0.0.1", "api.test");
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.DeleteEntry(api);
        changes.UpdateEntry(webDisabled, web);

        // The delete is on another entry, so it is crossed and the web pair cancels.
        changes.Changes.ShouldHaveSingleItem().ShouldBeOfType<DeleteEntryChange>()
            .Entry.ShouldBe(api);
    }

    [Fact]
    public void A_same_entry_reorder_between_two_updates_prevents_coalescing()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.ReorderEntry(webDisabled, 0);
        changes.UpdateEntry(webDisabled, web);

        // The reorder acts on the same entry, so it breaks the chain and the inverse update is kept.
        changes.Changes.Count.ShouldBe(3);
    }

    [Fact]
    public void An_inverse_update_coalesces_across_a_reorder_on_another_entry()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var api = new HostEntry("10.0.0.1", "api.test");
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.ReorderEntry(api, 0);
        changes.UpdateEntry(webDisabled, web);

        // The reorder is on another entry, so it is crossed and the web pair cancels.
        changes.Changes.ShouldHaveSingleItem().ShouldBeOfType<ReorderEntryChange>()
            .Entry.ShouldBe(api);
    }

    [Fact]
    public void A_same_entry_add_between_two_updates_prevents_coalescing()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.AddEntry(webDisabled);
        changes.UpdateEntry(webDisabled, web);

        // The add references the same entry, so it breaks the chain and the inverse update is kept.
        changes.Changes.Count.ShouldBe(3);
    }

    [Fact]
    public void An_inverse_update_coalesces_across_an_add_of_another_entry()
    {
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var other = new HostEntry("10.0.0.9", "other.test");
        var changes = new PendingChangeSet();

        changes.UpdateEntry(web, webDisabled);
        changes.AddEntry(other);
        changes.UpdateEntry(webDisabled, web);

        // The add is of another entry, so it is crossed and the web pair cancels.
        changes.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>()
            .Entry.ShouldBe(other);
    }

    [Fact]
    public void Cancelling_an_update_removes_only_the_preceding_update()
    {
        var other = new HostEntry("10.0.0.9", "other.test");
        var web = new HostEntry("127.0.0.1", "web.test");
        var webDisabled = web with { Status = HostEntryStatus.Disabled };
        var changes = new PendingChangeSet();

        changes.AddEntry(other);
        changes.UpdateEntry(web, webDisabled);
        changes.UpdateEntry(webDisabled, web);

        changes.Changes.ShouldHaveSingleItem()
            .ShouldBeOfType<AddEntryChange>()
            .Entry.ShouldBe(other);
    }

    [Fact]
    public void A_superseding_update_replaces_the_preceding_update_after_an_earlier_change()
    {
        var current = Grouping(NoCategory(new HostEntry("127.0.0.1", "web.test")));
        var changes = new PendingChangeSet();

        changes.AddEntry(new HostEntry("10.0.0.9", "new.test"));
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"), new HostEntry("10.0.0.1", "web.test"));
        changes.UpdateEntry(
            new HostEntry("10.0.0.1", "web.test"), new HostEntry("192.168.0.1", "web.test"));

        changes.Changes.Count.ShouldBe(2);
        changes.Changes[0].ShouldBeOfType<AddEntryChange>();
        var update = changes.Changes[1].ShouldBeOfType<UpdateEntryChange>();
        update.Target.ShouldBe(new HostEntry("127.0.0.1", "web.test"));
        update.Replacement.ShouldBe(new HostEntry("192.168.0.1", "web.test"));
        EntriesUnder(changes.ApplyTo(current), null).Select(entry => entry.Hostname)
            .ShouldBe(new[] { "web.test", "new.test" });
    }
}
