using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ApplyPendingChanges;
using SimpleHost.Application.UseCases.DiscardPendingChanges;
using SimpleHost.Application.UseCases.ExportCategory;
using SimpleHost.Application.UseCases.ExportConfiguration;
using SimpleHost.Application.UseCases.FilterEntriesByCategory;
using SimpleHost.Application.UseCases.ImportHostsFile;
using SimpleHost.Application.UseCases.PreviewPendingChanges;
using SimpleHost.Application.UseCases.RefreshSubscription;
using SimpleHost.Application.UseCases.RefreshSubscriptions;
using SimpleHost.Application.UseCases.SaveProfile;
using SimpleHost.Application.UseCases.SearchEntriesByHostname;
using SimpleHost.Application.UseCases.SearchEntriesByIpAddress;
using SimpleHost.Application.UseCases.SubscribeToRemoteHostsList;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;
using SimpleHost.Domain.Subscriptions;
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
    public void EditEntryField_stages_an_in_place_update_for_a_changed_ip_address()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test", "note") }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        var result = viewModel.EditEntryField(entry, HostEntryField.IpAddress, "127.0.0.2");

        result.IsAccepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();
        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Target.ShouldBe(new HostEntry("127.0.0.1", "api.test", "note"));
        change.Replacement.ShouldBe(new HostEntry("127.0.0.2", "api.test", "note"));
    }

    [Fact]
    public void EditEntryField_editing_the_hostname_keeps_the_other_fields_the_status_and_the_category()
    {
        var pendingChanges = new PendingChangeSet();
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[]
                {
                    new HostEntry("0.0.0.0", "blocked.test", "ad", HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        var entry = viewModel.Groups[0].Entries[0];

        var result = viewModel.EditEntryField(entry, HostEntryField.Hostname, "still-blocked.test");

        result.IsAccepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();
        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>();
        change.Replacement.ShouldBe(
            new HostEntry("0.0.0.0", "still-blocked.test", "ad", HostEntryStatus.Disabled));
    }

    [Fact]
    public void EditEntryField_stages_an_update_when_the_description_changes()
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

        var result = viewModel.EditEntryField(entry, HostEntryField.Comment, "new");

        result.IsAccepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();
        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("127.0.0.1", "api.test", "new"));
    }

    [Fact]
    public void EditEntryField_treats_a_blanked_description_as_no_description()
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

        var result = viewModel.EditEntryField(entry, HostEntryField.Comment, "   ");

        result.IsAccepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();
        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("127.0.0.1", "api.test", null));
    }

    [Fact]
    public void EditEntryField_rejects_an_invalid_ip_address_and_stages_nothing()
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

        var result = viewModel.EditEntryField(entry, HostEntryField.IpAddress, "not-an-ip");

        result.IsAccepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("'not-an-ip' is not a valid IP address.");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void EditEntryField_rejects_an_invalid_hostname_and_stages_nothing()
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

        var result = viewModel.EditEntryField(entry, HostEntryField.Hostname, "not a hostname");

        result.IsAccepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("'not a hostname' is not a valid hostname.");
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void EditEntryField_stages_nothing_when_the_value_is_unchanged()
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

        var result = viewModel.EditEntryField(entry, HostEntryField.Hostname, "api.test");

        result.IsAccepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void ValidateFieldValue_accepts_a_well_formed_ip_address_with_no_message()
        => MainViewModel.ValidateFieldValue(HostEntryField.IpAddress, "10.0.0.1").ShouldBeEmpty();

    [Fact]
    public void ValidateFieldValue_reports_the_reason_an_ip_address_is_invalid()
        => MainViewModel.ValidateFieldValue(HostEntryField.IpAddress, "not-an-ip")
            .ShouldBe("'not-an-ip' is not a valid IP address.");

    [Fact]
    public void ValidateFieldValue_reports_the_prompt_when_the_ip_address_is_blank()
        => MainViewModel.ValidateFieldValue(HostEntryField.IpAddress, "   ")
            .ShouldBe("Enter an IP address.");

    [Fact]
    public void ValidateFieldValue_accepts_a_well_formed_hostname_with_no_message()
        => MainViewModel.ValidateFieldValue(HostEntryField.Hostname, "api.test").ShouldBeEmpty();

    [Fact]
    public void ValidateFieldValue_reports_the_reason_a_hostname_is_invalid()
        => MainViewModel.ValidateFieldValue(HostEntryField.Hostname, "not a hostname")
            .ShouldBe("'not a hostname' is not a valid hostname.");

    [Fact]
    public void ValidateFieldValue_never_reports_an_error_for_the_comment_field()
    {
        MainViewModel.ValidateFieldValue(HostEntryField.Comment, "anything at all !!!").ShouldBeEmpty();
        MainViewModel.ValidateFieldValue(HostEntryField.Comment, null).ShouldBeEmpty();
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
    public void BeginAddEntry_opens_an_empty_draft_targeting_the_given_category()
    {
        var viewModel = BuildViewModel(new FakeHostsFileRepository());

        viewModel.BeginAddEntry("Development");

        viewModel.IsAddingEntry.ShouldBeTrue();
        viewModel.AddingEntryCategory.ShouldBe("Development");
        viewModel.NewEntry.IpAddress.ShouldBe(string.Empty);
        viewModel.NewEntry.Hostname.ShouldBe(string.Empty);
        viewModel.NewEntry.Category.ShouldBe("Development");
    }

    [Fact]
    public void BeginAddEntry_can_target_the_no_category_group()
    {
        var viewModel = BuildViewModel(new FakeHostsFileRepository());

        viewModel.BeginAddEntry(null);

        viewModel.IsAddingEntry.ShouldBeTrue();
        viewModel.AddingEntryCategory.ShouldBeNull();
    }

    [Fact]
    public void BeginAddEntry_announces_that_a_draft_is_being_added()
    {
        var viewModel = BuildViewModel(new FakeHostsFileRepository());
        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        viewModel.BeginAddEntry("Work");

        announced.ShouldContain(nameof(MainViewModel.IsAddingEntry));
        announced.ShouldContain(nameof(MainViewModel.AddingEntryCategory));
    }

    [Fact]
    public void CommitNewEntry_stages_a_valid_draft_as_a_pending_add_into_its_category()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.BeginAddEntry("Development");
        viewModel.NewEntry.IpAddress = "127.0.0.1";
        viewModel.NewEntry.Hostname = "api.test";
        viewModel.NewEntry.Comment = "local api";

        var committed = viewModel.CommitNewEntry();

        committed.ShouldBeTrue();
        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
        change.Entry.ShouldBe(new HostEntry("127.0.0.1", "api.test", "local api"));
        change.Category.ShouldBe("Development");
        viewModel.IsAddingEntry.ShouldBeFalse();
        viewModel.AddingEntryCategory.ShouldBeNull();
    }

    [Fact]
    public void CommitNewEntry_stages_a_no_category_draft_without_a_category()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.BeginAddEntry(null);
        viewModel.NewEntry.IpAddress = "127.0.0.1";
        viewModel.NewEntry.Hostname = "localhost";

        viewModel.CommitNewEntry();

        var change = pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<AddEntryChange>();
        change.Category.ShouldBeNull();
    }

    [Fact]
    public void CommitNewEntry_shows_the_added_entry_in_its_category_immediately()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Work", Array.Empty<HostEntry>()),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.BeginAddEntry("Work");
        viewModel.NewEntry.IpAddress = "10.0.0.1";
        viewModel.NewEntry.Hostname = "api.test";

        viewModel.CommitNewEntry();

        viewModel.Groups.Single(group => group.Name == "Work")
            .Entries.ShouldHaveSingleItem().Hostname.ShouldBe("api.test");
    }

    [Fact]
    public void CommitNewEntry_discards_an_empty_draft_without_staging_anything()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.BeginAddEntry("Work");

        var committed = viewModel.CommitNewEntry();

        committed.ShouldBeTrue();
        pendingChanges.HasChanges.ShouldBeFalse();
        viewModel.IsAddingEntry.ShouldBeFalse();
    }

    [Fact]
    public void CommitNewEntry_keeps_an_invalid_non_empty_draft_open_and_stages_nothing()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.BeginAddEntry("Work");
        viewModel.NewEntry.IpAddress = "not-an-ip";
        viewModel.NewEntry.Hostname = "api.test";

        var committed = viewModel.CommitNewEntry();

        committed.ShouldBeFalse();
        pendingChanges.HasChanges.ShouldBeFalse();
        viewModel.IsAddingEntry.ShouldBeTrue();
    }

    [Fact]
    public void CommitNewEntry_does_nothing_when_no_draft_is_open()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);

        var committed = viewModel.CommitNewEntry();

        committed.ShouldBeFalse();
        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void CancelAddEntry_closes_the_draft_without_staging_anything()
    {
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(new FakeHostsFileRepository(), pendingChanges);
        viewModel.BeginAddEntry("Work");
        viewModel.NewEntry.IpAddress = "10.0.0.1";
        viewModel.NewEntry.Hostname = "api.test";

        viewModel.CancelAddEntry();

        viewModel.IsAddingEntry.ShouldBeFalse();
        viewModel.AddingEntryCategory.ShouldBeNull();
        pendingChanges.HasChanges.ShouldBeFalse();
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

    [Fact]
    public void With_no_active_filter_the_visible_groups_mirror_the_underlying_groups()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.VisibleGroups.Select(group => group.DisplayName)
            .ShouldBe(new[] { "No category", "Development", "Production" });
        viewModel.VisibleGroups.SelectMany(group => group.Entries.Select(entry => entry.Hostname))
            .ShouldBe(new[] { "localhost", "router.home", "api.dev.test", "db.dev.test", "app.prod.test" });
        viewModel.HasActiveFilter.ShouldBeFalse();
    }

    [Fact]
    public void With_no_active_filter_the_visible_groups_keep_empty_groups()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Empty", Array.Empty<HostEntry>()),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.VisibleGroups.Select(group => group.DisplayName)
            .ShouldBe(new[] { "No category", "Empty" });
    }

    [Fact]
    public void A_search_query_narrows_the_visible_groups_to_entries_matching_by_hostname()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "dev";

        viewModel.VisibleGroups.Select(group => group.DisplayName).ShouldBe(new[] { "Development" });
        viewModel.VisibleGroups[0].Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "api.dev.test", "db.dev.test" });
        viewModel.HasActiveFilter.ShouldBeTrue();
    }

    [Fact]
    public void A_search_query_matches_the_hostname_case_insensitively_as_a_substring()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "PROD";

        viewModel.VisibleGroups.SelectMany(group => group.Entries.Select(entry => entry.Hostname))
            .ShouldBe(new[] { "app.prod.test" });
    }

    [Fact]
    public void A_search_query_narrows_the_visible_groups_to_entries_matching_by_ip_address()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "10.0.0";

        viewModel.VisibleGroups.SelectMany(group => group.Entries.Select(entry => entry.Hostname))
            .ShouldBe(new[] { "api.dev.test", "db.dev.test" });
    }

    [Fact]
    public void A_search_query_narrows_the_visible_groups_to_entries_matching_by_description()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("127.0.0.1", "localhost", "loopback interface"),
                    new HostEntry("192.168.1.1", "router.home", "the gateway"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        // "loopback" appears only in the first entry's description — neither its IP nor its hostname —
        // so the description search path is what keeps it and drops the other entry.
        viewModel.SearchQuery = "loopback";

        viewModel.VisibleGroups.SelectMany(group => group.Entries.Select(entry => entry.Hostname))
            .ShouldBe(new[] { "localhost" });
    }

    [Fact]
    public void A_search_query_matches_the_description_case_insensitively_as_a_substring()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("127.0.0.1", "localhost", "Loopback Interface"),
                    new HostEntry("192.168.1.1", "router.home", "the gateway"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.SearchQuery = "LOOP";

        viewModel.VisibleGroups.SelectMany(group => group.Entries.Select(entry => entry.Hostname))
            .ShouldBe(new[] { "localhost" });
    }

    [Fact]
    public void A_search_query_on_its_own_makes_the_filter_active()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "10.0.0";

        viewModel.HasActiveFilter.ShouldBeTrue();
    }

    [Fact]
    public void A_blank_search_query_on_its_own_leaves_the_filter_inactive()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "   ";

        viewModel.HasActiveFilter.ShouldBeFalse();
    }

    [Fact]
    public void A_query_that_matches_nothing_leaves_the_visible_groups_empty()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "no-such-host";

        viewModel.VisibleGroups.ShouldBeEmpty();
    }

    [Fact]
    public void A_blank_search_query_is_inactive_and_shows_every_entry()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();
        viewModel.SearchQuery = "dev";

        viewModel.SearchQuery = "   ";

        viewModel.VisibleGroups.Select(group => group.DisplayName)
            .ShouldBe(new[] { "No category", "Development", "Production" });
        viewModel.HasActiveFilter.ShouldBeFalse();
    }

    [Fact]
    public void Changing_a_query_raises_a_change_notification_for_the_active_filter_state()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        viewModel.SearchQuery = "dev";
        raised.ShouldContain(nameof(MainViewModel.HasActiveFilter));
    }

    [Fact]
    public void Searching_leaves_the_underlying_groups_unchanged()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "dev";

        viewModel.Groups.Select(group => group.DisplayName)
            .ShouldBe(new[] { "No category", "Development", "Production" });
        viewModel.Groups.SelectMany(group => group.Entries.Select(entry => entry.Hostname))
            .ShouldBe(new[] { "localhost", "router.home", "api.dev.test", "db.dev.test", "app.prod.test" });
    }

    [Fact]
    public void A_visible_entry_is_the_same_instance_as_in_the_underlying_groups()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.Load();

        viewModel.SearchQuery = "dev";

        var underlying = viewModel.Groups.Single(group => group.DisplayName == "Development").Entries[0];
        var visible = viewModel.VisibleGroups.Single(group => group.DisplayName == "Development").Entries[0];
        visible.ShouldBeSameAs(underlying);
    }

    [Fact]
    public void Refresh_re_applies_the_active_filter_to_the_freshly_read_data()
    {
        var repository = SampleRepository();
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.SearchQuery = "dev";

        repository.GroupedEntries = new[]
        {
            new CategoryGroup("Development", new[]
            {
                new HostEntry("10.0.0.1", "api.dev.test"),
                new HostEntry("10.0.0.9", "cache.dev.test"),
            }),
        };
        viewModel.Refresh();

        viewModel.VisibleGroups.SelectMany(group => group.Entries.Select(entry => entry.Hostname))
            .ShouldBe(new[] { "api.dev.test", "cache.dev.test" });
    }

    [Fact]
    public void SaveProfile_lists_the_saved_profile_capturing_the_currently_enabled_categories()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
                new CategoryGroup("Blocklist", new[]
                {
                    new HostEntry("0.0.0.0", "ads.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.SaveProfile("Work");

        var profile = viewModel.Profiles.ShouldHaveSingleItem();
        profile.Name.ShouldBe("Work");
        profile.EnabledCategories.ShouldBe(new string?[] { null, "Development" });
    }

    [Fact]
    public void SaveProfile_treats_a_category_with_any_active_entry_as_enabled()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[]
                {
                    new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
                    new HostEntry("10.0.0.2", "db.test", null, HostEntryStatus.Active),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.SaveProfile("Work");

        viewModel.Profiles.ShouldHaveSingleItem().EnabledCategories.ShouldBe(new[] { "Development" });
    }

    [Fact]
    public void SaveProfile_persists_the_profile_through_the_save_profile_use_case()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
            },
        };
        var profileStore = new FakeProfileStore();
        var viewModel = BuildViewModel(repository, profileStore: profileStore);
        viewModel.Load();

        viewModel.SaveProfile("  Work  ");

        var stored = profileStore.Saved.ShouldHaveSingleItem();
        stored.Name.ShouldBe("Work");
        stored.EnabledCategories.ShouldBe(new[] { "Development" });
    }

    [Fact]
    public void SaveProfile_replaces_a_profile_saved_under_the_same_name()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
                new CategoryGroup("Home", new[]
                {
                    new HostEntry("192.168.0.1", "nas.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.SaveProfile("Preset");

        repository.GroupedEntries = new[]
        {
            new CategoryGroup("Development", new[]
            {
                new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Home", new[] { new HostEntry("192.168.0.1", "nas.test") }),
        };
        viewModel.Refresh();
        viewModel.SaveProfile("Preset");

        var profile = viewModel.Profiles.ShouldHaveSingleItem();
        profile.Name.ShouldBe("Preset");
        profile.EnabledCategories.ShouldBe(new[] { "Home" });
    }

    [Fact]
    public void SaveProfile_saves_nothing_for_a_blank_name()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
            },
        };
        var profileStore = new FakeProfileStore();
        var viewModel = BuildViewModel(repository, profileStore: profileStore);
        viewModel.Load();

        viewModel.SaveProfile("   ");

        viewModel.Profiles.ShouldBeEmpty();
        profileStore.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void ActivateProfile_stages_enabling_the_listed_categories_and_disabling_the_rest()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
                new CategoryGroup("Blocklist", new[]
                {
                    new HostEntry("0.0.0.0", "ads.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SaveProfile("Work");

        // Flip the file so the profile now differs from the current state, then recall it.
        repository.GroupedEntries = new[]
        {
            new CategoryGroup("Development", new[]
            {
                new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Blocklist", new[] { new HostEntry("0.0.0.0", "ads.test") }),
        };
        viewModel.Refresh();

        viewModel.ActivateProfile("Work");

        pendingChanges.Changes.Count.ShouldBe(2);
        var enable = pendingChanges.Changes[0].ShouldBeOfType<UpdateEntryChange>();
        enable.Replacement.ShouldBe(new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Active));
        var disable = pendingChanges.Changes[1].ShouldBeOfType<UpdateEntryChange>();
        disable.Replacement.ShouldBe(new HostEntry("0.0.0.0", "ads.test", null, HostEntryStatus.Disabled));
    }

    [Fact]
    public void ActivateProfile_enables_the_no_category_group_when_the_profile_lists_it()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Development", new[]
                {
                    new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SaveProfile("Home");

        repository.GroupedEntries = new[]
        {
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "localhost", null, HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
        };
        viewModel.Refresh();

        viewModel.ActivateProfile("Home");

        pendingChanges.Changes.Count.ShouldBe(2);
        pendingChanges.Changes[0].ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("127.0.0.1", "localhost", null, HostEntryStatus.Active));
        pendingChanges.Changes[1].ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled));
    }

    [Fact]
    public void ActivateProfile_enables_every_listed_category_and_disables_every_unlisted_one()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
                new CategoryGroup("Home", new[] { new HostEntry("192.168.0.1", "nas.test") }),
                new CategoryGroup("Blocklist", new[]
                {
                    new HostEntry("0.0.0.0", "ads.test", null, HostEntryStatus.Disabled),
                }),
            },
        };
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SaveProfile("Both"); // captures the two active categories: Development and Home

        repository.GroupedEntries = new[]
        {
            new CategoryGroup("Development", new[]
            {
                new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Home", new[]
            {
                new HostEntry("192.168.0.1", "nas.test", null, HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Blocklist", new[] { new HostEntry("0.0.0.0", "ads.test") }),
        };
        viewModel.Refresh();

        viewModel.ActivateProfile("Both");

        pendingChanges.Changes.Count.ShouldBe(3);
        pendingChanges.Changes[0].ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Active));
        pendingChanges.Changes[1].ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("192.168.0.1", "nas.test", null, HostEntryStatus.Active));
        pendingChanges.Changes[2].ShouldBeOfType<UpdateEntryChange>()
            .Replacement.ShouldBe(new HostEntry("0.0.0.0", "ads.test", null, HostEntryStatus.Disabled));
    }

    [Fact]
    public void ActivateProfile_stages_nothing_for_an_unknown_profile_name()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
            },
        };
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();

        viewModel.ActivateProfile("Missing");

        pendingChanges.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void ActivateProfile_stages_changes_without_writing_to_the_file()
    {
        // The fake repository throws on every mutating operation, so staging changes (rather than
        // writing) proves the recall goes through the pending-change set, not straight to disk.
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Development", new[] { new HostEntry("10.0.0.1", "api.test") }),
            },
        };
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.SaveProfile("Work");

        repository.GroupedEntries = new[]
        {
            new CategoryGroup("Development", new[]
            {
                new HostEntry("10.0.0.1", "api.test", null, HostEntryStatus.Disabled),
            }),
        };
        viewModel.Refresh();

        viewModel.ActivateProfile("Work");

        pendingChanges.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void LoadPersistedProfiles_offers_the_persisted_profiles_for_recall()
    {
        var viewModel = BuildViewModel(SampleRepository());
        var snapshot = Profile.FromSnapshot("Default", "127.0.0.1 localhost\n",
            new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }) });
        var preset = Profile.Of("Work", new[] { "Development" });

        viewModel.LoadPersistedProfiles(new[] { snapshot, preset });

        viewModel.Profiles.Select(profile => profile.Name).ShouldBe(new[] { "Default", "Work" });
    }

    [Fact]
    public void LoadPersistedProfiles_replaces_whatever_the_list_held()
    {
        var viewModel = BuildViewModel(SampleRepository());
        viewModel.LoadPersistedProfiles(new[] { Profile.Of("Old", new[] { "Development" }) });

        viewModel.LoadPersistedProfiles(new[] { Profile.Of("New", new[] { "Production" }) });

        viewModel.Profiles.ShouldHaveSingleItem().Name.ShouldBe("New");
    }

    [Fact]
    public void ActivateProfile_stages_restoring_the_captured_content_for_a_snapshot_profile()
    {
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "edited.test") }),
                new CategoryGroup("Added", new[] { new HostEntry("10.0.0.9", "new.test") }),
            },
        };
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.LoadPersistedProfiles(new[]
        {
            Profile.FromSnapshot(
                "Default",
                "127.0.0.1 localhost\n",
                new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }) }),
        });

        viewModel.ActivateProfile("Default");

        pendingChanges.Changes.ShouldHaveSingleItem().ShouldBeOfType<RestoreContentChange>();
        viewModel.Groups.Select(group => group.Name).ShouldBe(new string?[] { null });
        viewModel.Groups[0].Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "localhost" });
    }

    [Fact]
    public void ActivateProfile_stages_the_snapshot_restore_without_writing_to_the_file()
    {
        // The fake repository throws on every entry-level mutating operation, so staging a restore
        // (rather than writing) proves the recall of a snapshot profile goes through the pending set.
        var repository = new FakeHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "edited.test") }),
            },
        };
        var pendingChanges = new PendingChangeSet();
        var viewModel = BuildViewModel(repository, pendingChanges);
        viewModel.Load();
        viewModel.LoadPersistedProfiles(new[]
        {
            Profile.FromSnapshot(
                "Default",
                "127.0.0.1 localhost\n",
                new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }) }),
        });

        viewModel.ActivateProfile("Default");

        pendingChanges.HasChanges.ShouldBeTrue();
    }

    private static FakeHostsFileRepository SampleRepository() => new()
    {
        GroupedEntries = new[]
        {
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "localhost"),
                new HostEntry("192.168.1.1", "router.home"),
            }),
            new CategoryGroup("Development", new[]
            {
                new HostEntry("10.0.0.1", "api.dev.test"),
                new HostEntry("10.0.0.2", "db.dev.test"),
            }),
            new CategoryGroup("Production", new[]
            {
                new HostEntry("172.16.0.1", "app.prod.test"),
            }),
        },
    };

    private static MainViewModel BuildViewModel(
        FakeHostsFileRepository repository,
        PendingChangeSet? pendingChanges = null,
        FakeProfileStore? profileStore = null) =>
        new(
            new ViewGroupedEntriesUseCase(repository),
            new ViewHostnameConflictsUseCase(repository),
            new ViewDuplicateEntriesUseCase(repository),
            new PreviewPendingChangesUseCase(repository),
            new ApplyPendingChangesUseCase(
                repository, new FakeElevationService(isElevated: true), new NoOpDnsCacheFlusher()),
            new DiscardPendingChangesUseCase(repository),
            new SearchEntriesByHostnameUseCase(repository),
            new SearchEntriesByIpAddressUseCase(repository),
            new FilterEntriesByCategoryUseCase(repository),
            new SaveProfileUseCase(profileStore ?? new FakeProfileStore()),
            new SubscribeToRemoteHostsListUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionsUseCase(
                new StubSubscriptionCatalog(), new StubRemoteHostsListFetcher(), repository),
            new ExportCategoryUseCase(repository, new StubHostsFileExporter()),
            new ExportConfigurationUseCase(repository, new StubHostsFileExporter()),
            new ImportHostsFileUseCase(new StubHostsFileImporter(), repository),
            pendingChanges ?? new PendingChangeSet());

    private sealed class StubRemoteHostsListFetcher : IRemoteHostsListFetcher
    {
        public IReadOnlyList<HostEntry> Fetch(string url) => throw new NotSupportedException();
    }

    private sealed class StubSubscriptionCatalog : ISubscriptionCatalog
    {
        public IReadOnlyList<Subscription> GetSubscriptions() => Array.Empty<Subscription>();
    }

    private sealed class StubHostsFileExporter : IHostsFileExporter
    {
        public void ExportCategory(CategoryGroup group, string destinationPath)
            => throw new NotSupportedException();

        public void ExportConfiguration(IReadOnlyList<CategoryGroup> groups, string destinationPath)
            => throw new NotSupportedException();
    }

    private sealed class StubHostsFileImporter : IHostsFileImporter
    {
        public IReadOnlyList<HostEntry> ImportEntries(string sourcePath) => throw new NotSupportedException();
    }

    private sealed class FakeProfileStore : IProfileStore
    {
        private readonly List<Profile> _saved = new();

        public IReadOnlyList<Profile> Saved => _saved;

        public void Save(Profile profile)
        {
            _saved.RemoveAll(existing => existing.Name == profile.Name);
            _saved.Add(profile);
        }

        public Profile? Find(string name) => _saved.FirstOrDefault(profile => profile.Name == name);

        public IReadOnlyList<Profile> GetAll() => _saved.ToList();
    }

    private sealed class FakeElevationService(bool isElevated) : IElevationService
    {
        public bool IsElevated { get; } = isElevated;
    }

    private sealed class NoOpDnsCacheFlusher : IDnsCacheFlusher
    {
        public void Flush()
        {
        }
    }

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

        // The search use cases read the flat entry list; keep it consistent with the grouped view
        // by flattening the groups in file order, so a test that sets GroupedEntries also gets a
        // matching GetEntries without a second, drift-prone setup.
        public IReadOnlyList<HostEntry> GetEntries()
            => GroupedEntries.SelectMany(group => group.Entries).ToList();
    }
}
