using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class MainViewModelTests
{
    [Fact]
    public void Load_exposes_the_no_category_group_first_then_each_category_in_file_order()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }),
                new CategoryGroup("Production", new[] { new HostEntry("10.0.0.1", "app.test") }),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        viewModel.Groups.Select(group => group.DisplayName)
            .ShouldBe(new[] { "No category", "Development", "Production" });
        viewModel.Groups[0].IsUncategorized.ShouldBeTrue();
        viewModel.Groups[0].Name.ShouldBeNull();
        viewModel.Groups[1].IsUncategorized.ShouldBeFalse();
        viewModel.Groups[1].Name.ShouldBe("Development");
    }

    [Fact]
    public void Load_projects_an_active_entry_with_its_address_hostname_comment_and_state()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("127.0.0.1", "localhost", "loopback", HostEntryStatus.Active),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        var entry = viewModel.Groups[0].Entries.ShouldHaveSingleItem();
        entry.IpAddress.ShouldBe("127.0.0.1");
        entry.Hostname.ShouldBe("localhost");
        entry.Comment.ShouldBe("loopback");
        entry.Status.ShouldBe(HostEntryStatus.Active);
        entry.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Load_marks_a_disabled_entry_as_not_active_and_carries_no_comment()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("0.0.0.0", "blocked.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        var entry = viewModel.Groups[0].Entries.ShouldHaveSingleItem();
        entry.Status.ShouldBe(HostEntryStatus.Disabled);
        entry.IsActive.ShouldBeFalse();
        entry.Comment.ShouldBeNull();
    }

    [Fact]
    public void Load_preserves_entry_order_within_a_group()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("127.0.0.1", "first.test"),
                    new HostEntry("127.0.0.2", "second.test"),
                    new HostEntry("127.0.0.3", "third.test"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        viewModel.Groups[0].Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "first.test", "second.test", "third.test" });
    }

    [Fact]
    public void Load_surfaces_the_hostname_conflict_warnings_for_the_current_file()
    {
        var repository = new FakeHostsFileRepository
        {
            ActiveEntries = new[]
            {
                new HostEntry("1.1.1.1", "conflict.test"),
                new HostEntry("2.2.2.2", "conflict.test"),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        viewModel.HasHostnameConflicts.ShouldBeTrue();
        viewModel.HostnameConflictWarnings.ShouldBe(new[]
        {
            "'conflict.test' is mapped to conflicting IP addresses: 1.1.1.1, 2.2.2.2.",
        });
    }

    [Fact]
    public void Load_reports_no_hostname_conflicts_when_every_hostname_is_consistent()
    {
        var repository = new FakeHostsFileRepository
        {
            ActiveEntries = new[] { new HostEntry("1.1.1.1", "solo.test") },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        viewModel.HasHostnameConflicts.ShouldBeFalse();
        viewModel.HostnameConflictWarnings.ShouldBeEmpty();
    }

    [Fact]
    public void Load_surfaces_the_duplicate_entry_warnings_for_the_current_file()
    {
        var repository = new FakeHostsFileRepository
        {
            ActiveEntries = new[]
            {
                new HostEntry("9.9.9.9", "dupe.test"),
                new HostEntry("9.9.9.9", "dupe.test"),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        viewModel.HasDuplicateEntries.ShouldBeTrue();
        viewModel.DuplicateEntryWarnings.ShouldBe(new[]
        {
            "'9.9.9.9 dupe.test' is duplicated: it appears 2 times.",
        });
    }

    [Fact]
    public void Load_reports_no_duplicates_when_every_entry_is_unique()
    {
        var repository = new FakeHostsFileRepository
        {
            ActiveEntries = new[]
            {
                new HostEntry("1.1.1.1", "a.test"),
                new HostEntry("2.2.2.2", "b.test"),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.Load();

        viewModel.HasDuplicateEntries.ShouldBeFalse();
        viewModel.DuplicateEntryWarnings.ShouldBeEmpty();
    }

    [Fact]
    public void Refresh_rebuilds_the_grouped_view_to_match_the_changed_file()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        repository.GroupedEntries = new[]
        {
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "localhost"),
                new HostEntry("127.0.0.2", "added.test"),
            }),
            new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
        };
        viewModel.Refresh();

        viewModel.Groups.Count.ShouldBe(2);
        viewModel.Groups[0].Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "localhost", "added.test" });
        viewModel.Groups[1].DisplayName.ShouldBe("Development");
    }

    [Fact]
    public void Refresh_clears_warnings_that_the_file_no_longer_has()
    {
        var repository = new FakeHostsFileRepository
        {
            ActiveEntries = new[]
            {
                new HostEntry("1.1.1.1", "conflict.test"),
                new HostEntry("2.2.2.2", "conflict.test"),
                new HostEntry("9.9.9.9", "dupe.test"),
                new HostEntry("9.9.9.9", "dupe.test"),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.HasHostnameConflicts.ShouldBeTrue();
        viewModel.HasDuplicateEntries.ShouldBeTrue();

        repository.ActiveEntries = new[] { new HostEntry("1.1.1.1", "clean.test") };
        viewModel.Refresh();

        viewModel.HasHostnameConflicts.ShouldBeFalse();
        viewModel.HostnameConflictWarnings.ShouldBeEmpty();
        viewModel.HasDuplicateEntries.ShouldBeFalse();
        viewModel.DuplicateEntryWarnings.ShouldBeEmpty();
    }

    [Fact]
    public void The_refresh_command_can_always_execute()
    {
        var viewModel = BuildViewModel(new FakeHostsFileRepository());

        viewModel.RefreshCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void The_refresh_command_reads_the_file_into_the_grouped_view()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);

        viewModel.RefreshCommand.Execute(null);

        viewModel.Groups.ShouldHaveSingleItem()
            .Entries.ShouldHaveSingleItem()
            .Hostname.ShouldBe("localhost");
    }

    [Fact]
    public void AddEntry_records_an_add_pending_change_with_the_new_entry_and_category()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        viewModel.AddEntry("127.0.0.1", "api.test", "local api", "Development");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
        change.Entry.ShouldBe(new HostEntry("127.0.0.1", "api.test", "local api"));
        change.Category.ShouldBe("Development");
    }

    [Fact]
    public void AddEntry_defaults_a_new_entry_to_no_comment_and_no_category()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        viewModel.AddEntry("10.0.0.1", "app.test");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
        change.Entry.ShouldBe(new HostEntry("10.0.0.1", "app.test"));
        change.Entry.Comment.ShouldBeNull();
        change.Category.ShouldBeNull();
    }

    [Fact]
    public void AddEntry_stages_the_change_without_writing_to_the_file()
    {
        // The fake repository throws on every mutating operation, so a passing recording
        // proves the add was staged as a pending change rather than written to disk.
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        viewModel.AddEntry("127.0.0.1", "api.test");

        pendingChanges.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void EditEntry_records_an_update_pending_change_replacing_the_entry_in_place()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "old") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.EditEntry(entry, "127.0.0.2", "api.test", "new", "Development");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("127.0.0.1", "api.test", "old"));
        change.Replacement.ShouldBe(new HostEntry("127.0.0.2", "api.test", "new"));
    }

    [Fact]
    public void EditEntry_preserves_the_entry_status_when_replacing()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("0.0.0.0", "blocked.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.EditEntry(entry, "0.0.0.0", "still-blocked.test", null, null);

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Replacement.ShouldBe(
            new HostEntry("0.0.0.0", "still-blocked.test", null, HostEntryStatus.Disabled));
    }

    [Fact]
    public void EditEntry_records_a_move_pending_change_when_only_the_category_changes()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "keep") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.EditEntry(entry, "127.0.0.1", "api.test", "keep", "Production");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<MoveEntryChange>();
        change.Entry.ShouldBe(new HostEntry("127.0.0.1", "api.test", "keep"));
        change.TargetCategory.ShouldBe("Production");
    }

    [Fact]
    public void EditEntry_records_update_then_move_when_content_and_category_both_change()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "old") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.EditEntry(entry, "10.0.0.9", "api.test", "new", "Production");

        pendingChanges.Changes.Count.ShouldBe(2);
        var update = pendingChanges.Changes[0].ShouldBeOfType<UpdateEntryChange>();
        update.Target.ShouldBe(new HostEntry("127.0.0.1", "api.test", "old"));
        update.Replacement.ShouldBe(new HostEntry("10.0.0.9", "api.test", "new"));
        var move = pendingChanges.Changes[1].ShouldBeOfType<MoveEntryChange>();
        move.Entry.ShouldBe(new HostEntry("10.0.0.9", "api.test", "new"));
        move.TargetCategory.ShouldBe("Production");
    }

    [Fact]
    public void EditEntry_records_nothing_when_neither_content_nor_category_change()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "same") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.EditEntry(entry, "127.0.0.1", "api.test", "same", "Development");

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void EditEntry_moves_an_uncategorized_entry_into_a_named_category()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.EditEntry(entry, "127.0.0.1", "localhost", null, "Development");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<MoveEntryChange>();
        change.TargetCategory.ShouldBe("Development");
    }

    [Fact]
    public void EditEntry_treats_an_entry_absent_from_the_groups_as_uncategorized()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        var entry = new HostEntryViewModel(new HostEntry("127.0.0.1", "orphan.test"));

        // Not present in any group, so its current category is "No category"; moving it to a
        // named category still records a move, while leaving it uncategorized records nothing.
        viewModel.EditEntry(entry, "127.0.0.1", "orphan.test", null, null);
        pendingChanges.HasChanges.ShouldBeFalse();

        viewModel.EditEntry(entry, "127.0.0.1", "orphan.test", null, "Development");
        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<MoveEntryChange>()
            .TargetCategory.ShouldBe("Development");
    }

    [Fact]
    public void DeleteEntry_records_a_delete_pending_change_when_the_view_confirms()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost", "loopback") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];
        viewModel.DeleteConfirmation = _ => true;

        viewModel.DeleteEntry(entry);

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<DeleteEntryChange>()
            .Entry.ShouldBe(new HostEntry("127.0.0.1", "localhost", "loopback"));
    }

    [Fact]
    public void DeleteEntry_records_nothing_when_the_view_declines_the_confirmation()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];
        viewModel.DeleteConfirmation = _ => false;

        viewModel.DeleteEntry(entry);

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DeleteEntry_records_nothing_when_the_view_supplies_no_confirmation()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.DeleteEntry(entry);

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DeleteEntry_offers_the_entry_being_deleted_to_the_confirmation()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];
        HostEntryViewModel? confirmed = null;
        viewModel.DeleteConfirmation = candidate =>
        {
            confirmed = candidate;
            return true;
        };

        viewModel.DeleteEntry(entry);

        confirmed.ShouldBeSameAs(entry);
    }

    [Fact]
    public void SaveNewEntry_stages_the_drafted_entry_when_the_draft_is_valid()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.NewEntry.IpAddress = "127.0.0.1";
        viewModel.NewEntry.Hostname = "api.test";
        viewModel.NewEntry.Comment = "local api";
        viewModel.NewEntry.Category = "Development";

        viewModel.SaveNewEntry();

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
        change.Entry.ShouldBe(new HostEntry("127.0.0.1", "api.test", "local api"));
        change.Category.ShouldBe("Development");
    }

    [Fact]
    public void SaveNewEntry_clears_the_draft_after_staging_it()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.NewEntry.IpAddress = "127.0.0.1";
        viewModel.NewEntry.Hostname = "api.test";

        viewModel.SaveNewEntry();

        viewModel.NewEntry.IpAddress.ShouldBe(string.Empty);
        viewModel.NewEntry.Hostname.ShouldBe(string.Empty);
        viewModel.NewEntry.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void SaveNewEntry_treats_a_blank_comment_and_category_as_absent()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.NewEntry.IpAddress = "127.0.0.1";
        viewModel.NewEntry.Hostname = "api.test";
        viewModel.NewEntry.Comment = "   ";
        viewModel.NewEntry.Category = "   ";

        viewModel.SaveNewEntry();

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
        change.Entry.Comment.ShouldBeNull();
        change.Category.ShouldBeNull();
    }

    [Fact]
    public void SaveNewEntry_stages_and_clears_nothing_when_the_draft_is_invalid()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.NewEntry.IpAddress = "not-an-ip";
        viewModel.NewEntry.Hostname = "api.test";

        viewModel.SaveNewEntry();

        pendingChanges.HasChanges.ShouldBeFalse();
        viewModel.NewEntry.IpAddress.ShouldBe("not-an-ip");
    }

    [Fact]
    public void BeginEditEntry_loads_the_entry_and_its_current_category_into_the_edit_draft()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "old") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.BeginEditEntry(entry);

        viewModel.EditDraft.IpAddress.ShouldBe("127.0.0.1");
        viewModel.EditDraft.Hostname.ShouldBe("api.test");
        viewModel.EditDraft.Comment.ShouldBe("old");
        viewModel.EditDraft.Category.ShouldBe("Development");
        viewModel.EditDraft.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void SaveEditedEntry_stages_the_edits_against_the_entry_being_edited()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "old") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.BeginEditEntry(viewModel.Groups[0].Entries[0]);
        viewModel.EditDraft.IpAddress = "127.0.0.2";
        viewModel.EditDraft.Comment = "new";

        viewModel.SaveEditedEntry();

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("127.0.0.1", "api.test", "old"));
        change.Replacement.ShouldBe(new HostEntry("127.0.0.2", "api.test", "new"));
    }

    [Fact]
    public void SaveEditedEntry_treats_a_blank_comment_and_category_as_absent()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "old") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.BeginEditEntry(viewModel.Groups[0].Entries[0]);
        viewModel.EditDraft.Comment = "   ";
        viewModel.EditDraft.Category = "   ";

        viewModel.SaveEditedEntry();

        // Comment cleared to null (an update) and category blanked to "No category" (a move out of
        // Development), so both a replacement and a move are staged.
        var update = pendingChanges.Changes[0].ShouldBeOfType<UpdateEntryChange>();
        update.Replacement.Comment.ShouldBeNull();
        var move = pendingChanges.Changes[1].ShouldBeOfType<MoveEntryChange>();
        move.TargetCategory.ShouldBeNull();
    }

    [Fact]
    public void SaveEditedEntry_stages_nothing_when_the_draft_is_invalid()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "api.test") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.BeginEditEntry(viewModel.Groups[0].Entries[0]);
        viewModel.EditDraft.Hostname = "bad_host";

        viewModel.SaveEditedEntry();

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void SaveEditedEntry_stages_nothing_when_no_edit_has_begun()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.EditDraft.IpAddress = "127.0.0.1";
        viewModel.EditDraft.Hostname = "api.test";

        viewModel.SaveEditedEntry();

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void CreateCategory_records_a_create_category_pending_change()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        viewModel.CreateCategory("Development");

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<CreateCategoryChange>()
            .Name.ShouldBe("Development");
    }

    [Fact]
    public void RenameCategory_records_a_rename_category_pending_change()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        viewModel.RenameCategory("Development", "Staging");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<RenameCategoryChange>();
        change.CurrentName.ShouldBe("Development");
        change.NewName.ShouldBe("Staging");
    }

    [Fact]
    public void DeleteCategory_records_a_delete_category_pending_change()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        viewModel.DeleteCategory("Development");

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<DeleteCategoryChange>()
            .Name.ShouldBe("Development");
    }

    [Fact]
    public void MoveEntry_records_a_move_pending_change_into_the_target_category()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.MoveEntry(entry, "Production");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<MoveEntryChange>();
        change.Entry.ShouldBe(new HostEntry("127.0.0.1", "api.test"));
        change.TargetCategory.ShouldBe("Production");
    }

    [Fact]
    public void MoveEntry_preserves_a_disabled_entry_status_when_moving()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("0.0.0.0", "blocked.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.MoveEntry(entry, "Blocklist");

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<MoveEntryChange>()
            .Entry.ShouldBe(new HostEntry("0.0.0.0", "blocked.test", null, HostEntryStatus.Disabled));
    }

    [Fact]
    public void MoveEntry_moves_an_entry_out_into_no_category()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.MoveEntry(entry, null);

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<MoveEntryChange>()
            .TargetCategory.ShouldBeNull();
    }

    [Fact]
    public void MoveEntry_records_nothing_when_the_entry_is_already_in_the_target_category()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.MoveEntry(entry, "Development");

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void ReorderEntry_records_a_reorder_pending_change_at_the_target_index()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[]
                {
                    new HostEntry("127.0.0.1", "api.test"),
                    new HostEntry("127.0.0.2", "db.test"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[1];

        viewModel.ReorderEntry(entry, 0);

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<ReorderEntryChange>();
        change.Entry.ShouldBe(new HostEntry("127.0.0.2", "db.test"));
        change.TargetIndex.ShouldBe(0);
    }

    [Fact]
    public void Category_and_move_commands_stage_changes_without_writing_to_the_file()
    {
        // The fake repository throws on every mutating operation, so passing recordings prove the
        // commands staged pending changes rather than writing to disk.
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        viewModel.CreateCategory("Staging");
        viewModel.RenameCategory("Development", "Dev");
        viewModel.MoveEntry(entry, "Staging");
        viewModel.ReorderEntry(entry, 0);
        viewModel.DeleteCategory("Staging");

        pendingChanges.Changes.Count.ShouldBe(5);
    }

    [Fact]
    public void EnableEntry_stages_an_update_to_active_for_a_disabled_entry()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("0.0.0.0", "blocked.test", "note", HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.EnableEntry(viewModel.Groups[0].Entries[0]);

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("0.0.0.0", "blocked.test", "note", HostEntryStatus.Disabled));
        change.Replacement.ShouldBe(new HostEntry("0.0.0.0", "blocked.test", "note", HostEntryStatus.Active));
    }

    [Fact]
    public void EnableEntry_stages_nothing_when_the_entry_is_already_active()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.EnableEntry(viewModel.Groups[0].Entries[0]);

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DisableEntry_stages_an_update_to_disabled_for_an_active_entry()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test", "note") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.DisableEntry(viewModel.Groups[0].Entries[0]);

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("127.0.0.1", "web.test", "note", HostEntryStatus.Active));
        change.Replacement.ShouldBe(new HostEntry("127.0.0.1", "web.test", "note", HostEntryStatus.Disabled));
    }

    [Fact]
    public void DisableEntry_stages_nothing_when_the_entry_is_already_disabled()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("0.0.0.0", "blocked.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.DisableEntry(viewModel.Groups[0].Entries[0]);

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void EnableCategory_stages_enabling_only_the_disabled_entries_of_the_named_category()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Work", new[]
                {
                    new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
                    new HostEntry("10.0.0.2", "db.test", null, HostEntryStatus.Active),
                }),
                new CategoryGroup("Home", new[]
                {
                    new HostEntry("192.168.0.1", "nas.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.EnableCategory("Work");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled));
        change.Replacement.ShouldBe(new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Active));
    }

    [Fact]
    public void DisableCategory_stages_disabling_only_the_active_entries_of_the_named_category()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Work", new[]
                {
                    new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Active),
                    new HostEntry("10.0.0.2", "db.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.DisableCategory("Work");

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Active));
        change.Replacement.ShouldBe(new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled));
    }

    [Fact]
    public void EnableCategory_targets_the_no_category_group_when_the_name_is_null()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("127.0.0.1", "localhost", null, HostEntryStatus.Disabled),
                }),
                new CategoryGroup("Work", new[]
                {
                    new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.EnableCategory(null);

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("127.0.0.1", "localhost", null, HostEntryStatus.Active));
    }

    [Fact]
    public void EnableCategory_stages_nothing_for_a_category_that_is_not_shown()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Work", new[]
                {
                    new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.EnableCategory("Missing");

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void EnableSelectedEntries_stages_enabling_each_selected_entry()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("0.0.0.0", "a.test", null, HostEntryStatus.Disabled),
                    new HostEntry("0.0.0.0", "b.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[0]);
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[1]);

        viewModel.EnableSelectedEntries();

        pendingChanges.Changes.Count.ShouldBe(2);
        pendingChanges.Changes.ShouldAllBe(change =>
            ((UpdateEntryChange)change).Replacement.Status == HostEntryStatus.Active);
    }

    [Fact]
    public void DisableSelectedEntries_stages_disabling_each_selected_entry()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("1.1.1.1", "a.test"),
                    new HostEntry("2.2.2.2", "b.test"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[0]);
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[1]);

        viewModel.DisableSelectedEntries();

        pendingChanges.Changes.Count.ShouldBe(2);
        pendingChanges.Changes.ShouldAllBe(change =>
            ((UpdateEntryChange)change).Replacement.Status == HostEntryStatus.Disabled);
    }

    [Fact]
    public void DeleteSelectedEntries_stages_deleting_each_selected_entry()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("1.1.1.1", "a.test"),
                    new HostEntry("2.2.2.2", "b.test"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[0]);
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[1]);

        viewModel.DeleteSelectedEntries();

        pendingChanges.Changes.Count.ShouldBe(2);
        pendingChanges.Changes.Select(change => ((DeleteEntryChange)change).Entry.Hostname)
            .ShouldBe(new[] { "a.test", "b.test" });
    }

    [Fact]
    public void MoveSelectedEntriesToCategory_stages_moving_each_selected_entry()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Work", new[]
                {
                    new HostEntry("1.1.1.1", "a.test"),
                    new HostEntry("2.2.2.2", "b.test"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[0]);
        viewModel.SelectedEntries.Add(viewModel.Groups[0].Entries[1]);

        viewModel.MoveSelectedEntriesToCategory("Home");

        pendingChanges.Changes.Count.ShouldBe(2);
        pendingChanges.Changes.ShouldAllBe(change =>
            ((MoveEntryChange)change).TargetCategory == "Home");
    }

    [Fact]
    public void Bulk_commands_stage_nothing_for_an_empty_selection()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        viewModel.EnableSelectedEntries();
        viewModel.DisableSelectedEntries();
        viewModel.DeleteSelectedEntries();
        viewModel.MoveSelectedEntriesToCategory("Work");

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    private static MainViewModel BuildViewModel(
        FakeHostsFileRepository repository, PendingChangeSet? pendingChanges = null) =>
        new(
            new ViewGroupedEntriesUseCase(repository),
            new ViewHostnameConflictsUseCase(repository),
            new ViewDuplicateEntriesUseCase(repository),
            pendingChanges ?? new PendingChangeSet());

    private sealed class FakeHostsFileRepository : IHostsFileRepository
    {
        public IReadOnlyList<CategoryGroup> GroupedEntries { get; set; } = Array.Empty<CategoryGroup>();

        public IReadOnlyList<HostEntry> ActiveEntries { get; set; } = Array.Empty<HostEntry>();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => GroupedEntries;

        public IReadOnlyList<HostEntry> GetActiveEntries() => ActiveEntries;

        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();
    }
}
