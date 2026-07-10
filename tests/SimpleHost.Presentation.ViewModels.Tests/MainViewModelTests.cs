using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
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

    private static MainViewModel BuildViewModel(FakeHostsFileRepository repository) =>
        new(
            new ViewGroupedEntriesUseCase(repository),
            new ViewHostnameConflictsUseCase(repository),
            new ViewDuplicateEntriesUseCase(repository));

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
