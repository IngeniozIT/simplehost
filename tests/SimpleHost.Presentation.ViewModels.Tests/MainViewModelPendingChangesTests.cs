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
using SimpleHost.Application.UseCases.RemoveProfile;
using SimpleHost.Application.UseCases.RenameProfile;
using SimpleHost.Application.UseCases.SaveProfile;
using SimpleHost.Application.UseCases.SearchEntriesByHostname;
using SimpleHost.Application.UseCases.SearchEntriesByIpAddress;
using SimpleHost.Application.UseCases.SubscribeToRemoteHostsList;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Preview;
using SimpleHost.Domain.Profiles;
using SimpleHost.Domain.Subscriptions;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class MainViewModelPendingChangesTests
{
    [Fact]
    public void Has_no_pending_changes_before_anything_is_staged()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());

        viewModel.PendingChangeCount.ShouldBe(0);
        viewModel.HasPendingChanges.ShouldBeFalse();
    }

    [Fact]
    public void Reports_the_number_of_pending_changes_staged()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());

        viewModel.AddEntry("127.0.0.1", "a.test");
        viewModel.AddEntry("127.0.0.2", "b.test");

        viewModel.PendingChangeCount.ShouldBe(2);
        viewModel.HasPendingChanges.ShouldBeTrue();
    }

    [Fact]
    public void Announces_the_pending_count_and_flag_when_a_change_is_staged()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());
        var announced = new List<string>();
        viewModel.PropertyChanged += (_, e) => announced.Add(e.PropertyName!);

        viewModel.AddEntry("127.0.0.1", "a.test");

        announced.ShouldContain(nameof(MainViewModel.PendingChangeCount));
        announced.ShouldContain(nameof(MainViewModel.HasPendingChanges));
    }

    [Fact]
    public void Enables_the_apply_discard_and_preview_commands_once_a_change_is_staged()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());
        var applyRaised = false;
        var discardRaised = false;
        var previewRaised = false;
        viewModel.ApplyCommand.CanExecuteChanged += (_, _) => applyRaised = true;
        viewModel.DiscardCommand.CanExecuteChanged += (_, _) => discardRaised = true;
        viewModel.PreviewCommand.CanExecuteChanged += (_, _) => previewRaised = true;

        viewModel.AddEntry("127.0.0.1", "a.test");

        viewModel.ApplyCommand.CanExecute(null).ShouldBeTrue();
        viewModel.DiscardCommand.CanExecute(null).ShouldBeTrue();
        viewModel.PreviewCommand.CanExecute(null).ShouldBeTrue();
        applyRaised.ShouldBeTrue();
        discardRaised.ShouldBeTrue();
        previewRaised.ShouldBeTrue();
    }

    [Fact]
    public void Disables_the_apply_discard_and_preview_commands_when_nothing_is_pending()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());

        viewModel.ApplyCommand.CanExecute(null).ShouldBeFalse();
        viewModel.DiscardCommand.CanExecute(null).ShouldBeFalse();
        viewModel.PreviewCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Preview_shows_the_diff_of_what_the_file_will_look_like_once_applied()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.AddEntry("10.0.0.9", "new.test");

        viewModel.PreviewCommand.Execute(null);

        var diff = viewModel.PreviewDiff.ShouldNotBeNull();
        diff.HasChanges.ShouldBeTrue();
        diff.Lines.Select(line => (line.Kind, line.Placement.Entry.Hostname)).ShouldBe(new[]
        {
            (HostsFileDiffLineKind.Unchanged, "localhost"),
            (HostsFileDiffLineKind.Added, "new.test"),
        });
    }

    [Fact]
    public void Preview_does_not_write_anything_or_alter_the_pending_changes()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.AddEntry("10.0.0.9", "new.test");

        viewModel.PreviewCommand.Execute(null);

        repository.Operations.ShouldBeEmpty();
        viewModel.PendingChangeCount.ShouldBe(1);
    }

    [Fact]
    public void Apply_writes_every_pending_change_through_the_repository_in_order()
    {
        var repository = new RecordingHostsFileRepository();
        var viewModel = BuildViewModel(repository);
        viewModel.AddEntry("10.0.0.1", "api.test", category: null);
        viewModel.CreateCategory("Work");

        viewModel.ApplyCommand.Execute(null);

        repository.Operations.ShouldBe(new[]
        {
            "Add HostEntry { IpAddress = 10.0.0.1, Hostname = api.test, Comment = , Status = Active } cat=<none>",
            "CreateCategory Work",
        });
    }

    [Fact]
    public void Apply_clears_the_pending_changes_once_they_are_written()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());
        viewModel.AddEntry("10.0.0.1", "api.test");

        viewModel.ApplyPendingChanges();

        viewModel.PendingChangeCount.ShouldBe(0);
        viewModel.HasPendingChanges.ShouldBeFalse();
    }

    [Fact]
    public void Apply_refreshes_the_grouped_view_to_match_the_saved_file()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.AddEntry("127.0.0.2", "added.test");

        // The apply writes through the repository; the saved file now holds the extra entry.
        repository.GroupedEntries = new[]
        {
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "localhost"),
                new HostEntry("127.0.0.2", "added.test"),
            }),
        };
        viewModel.ApplyPendingChanges();

        viewModel.Groups.ShouldHaveSingleItem()
            .Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "localhost", "added.test" });
    }

    [Fact]
    public void Apply_does_not_flush_the_dns_cache_by_default()
    {
        var flusher = new RecordingDnsCacheFlusher();
        var viewModel = BuildViewModel(
            new RecordingHostsFileRepository(), dnsCacheFlusher: flusher);
        viewModel.AddEntry("10.0.0.1", "api.test");

        viewModel.ApplyPendingChanges();

        flusher.FlushCount.ShouldBe(0);
    }

    [Fact]
    public void Apply_flushes_the_dns_cache_when_the_flush_after_saving_setting_is_enabled()
    {
        var flusher = new RecordingDnsCacheFlusher();
        var viewModel = BuildViewModel(
            new RecordingHostsFileRepository(), dnsCacheFlusher: flusher);
        viewModel.FlushDnsAfterSaving = true;
        viewModel.AddEntry("10.0.0.1", "api.test");

        viewModel.ApplyPendingChanges();

        flusher.FlushCount.ShouldBe(1);
    }

    [Fact]
    public void The_flush_dns_after_saving_setting_is_off_by_default()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());

        viewModel.FlushDnsAfterSaving.ShouldBeFalse();
    }

    [Fact]
    public void Toggling_the_flush_dns_after_saving_setting_raises_a_change_notification()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        viewModel.FlushDnsAfterSaving = true;

        raised.ShouldContain(nameof(MainViewModel.FlushDnsAfterSaving));
    }

    [Fact]
    public void Apply_clears_the_preview_diff()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[] { new CategoryGroup(null, Array.Empty<HostEntry>()) },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.AddEntry("10.0.0.1", "api.test");
        viewModel.PreviewCommand.Execute(null);
        viewModel.PreviewDiff.ShouldNotBeNull();

        viewModel.ApplyPendingChanges();

        viewModel.PreviewDiff.ShouldBeNull();
    }

    [Fact]
    public void Apply_without_elevation_writes_nothing_and_keeps_the_changes_pending()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository, isElevated: false);
        viewModel.Load();
        viewModel.AddEntry("127.0.0.2", "added.test");
        viewModel.PreviewCommand.Execute(null);
        var previewBeforeApply = viewModel.PreviewDiff.ShouldNotBeNull();
        var elevationRequested = false;
        viewModel.ElevationRequired = () => elevationRequested = true;

        viewModel.ApplyPendingChanges();

        elevationRequested.ShouldBeTrue();
        repository.Operations.ShouldBeEmpty();
        viewModel.PendingChangeCount.ShouldBe(1);
        // The refused apply leaves the pending projection intact: the staged addition is still shown
        // alongside the on-disk entry, not reverted to the on-disk state.
        viewModel.Groups.ShouldHaveSingleItem()
            .Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "localhost", "added.test" });
        // The refused apply must not fall through to the refresh/clear that a successful apply does:
        // the pending preview is left intact for the retry.
        viewModel.PreviewDiff.ShouldBeSameAs(previewBeforeApply);
    }

    [Fact]
    public void Apply_without_elevation_and_without_a_callback_leaves_the_changes_pending()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository(), isElevated: false);
        viewModel.AddEntry("10.0.0.1", "api.test");

        Should.NotThrow(() => viewModel.ApplyPendingChanges());

        viewModel.PendingChangeCount.ShouldBe(1);
    }

    [Fact]
    public void Discard_clears_the_pending_changes_without_writing_anything()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.AddEntry("127.0.0.2", "added.test");

        viewModel.DiscardCommand.Execute(null);

        viewModel.PendingChangeCount.ShouldBe(0);
        viewModel.HasPendingChanges.ShouldBeFalse();
        repository.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void Discard_refreshes_the_grouped_view_to_match_the_on_disk_file()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.AddEntry("127.0.0.2", "added.test");

        viewModel.DiscardPendingChanges();

        viewModel.Groups.Select(group => group.DisplayName)
            .ShouldBe(new[] { "No category", "Work" });
        viewModel.Groups[0].Entries.ShouldHaveSingleItem().Hostname.ShouldBe("localhost");
    }

    [Fact]
    public void Discard_clears_the_preview_diff()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[] { new CategoryGroup(null, Array.Empty<HostEntry>()) },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.AddEntry("10.0.0.1", "api.test");
        viewModel.PreviewCommand.Execute(null);
        viewModel.PreviewDiff.ShouldNotBeNull();

        viewModel.DiscardPendingChanges();

        viewModel.PreviewDiff.ShouldBeNull();
    }

    [Fact]
    public void Staging_an_addition_shows_it_in_the_grouped_view_immediately()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.AddEntry("10.0.0.9", "added.test");

        viewModel.Groups.ShouldHaveSingleItem()
            .Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "localhost", "added.test" });
        viewModel.VisibleGroups.ShouldHaveSingleItem()
            .Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "localhost", "added.test" });
    }

    [Fact]
    public void Staging_a_status_flip_shows_the_entry_disabled_in_the_view_immediately()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.DisableEntry(viewModel.Groups[0].Entries[0]);

        var entry = viewModel.Groups[0].Entries.ShouldHaveSingleItem();
        entry.Hostname.ShouldBe("web.test");
        entry.Status.ShouldBe(HostEntryStatus.Disabled);
    }

    [Fact]
    public void Staging_an_edit_shows_the_updated_entry_in_the_view_immediately()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup("Work", new[] { new HostEntry("127.0.0.1", "api.test", "old") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.EditEntry(viewModel.Groups[0].Entries[0], "127.0.0.2", "api.test", "new", "Work");

        var entry = viewModel.Groups[0].Entries.ShouldHaveSingleItem();
        entry.IpAddress.ShouldBe("127.0.0.2");
        entry.Comment.ShouldBe("new");
    }

    [Fact]
    public void Staging_a_move_shows_the_entry_under_its_new_category_immediately()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Work", Array.Empty<HostEntry>()),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.MoveEntry(viewModel.Groups[0].Entries[0], "Work");

        viewModel.Groups.Single(group => group.Name == "Work")
            .Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "localhost" });
        viewModel.Groups.Single(group => group.IsUncategorized).Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Staging_a_deletion_removes_the_entry_from_the_view_immediately()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[]
                {
                    new HostEntry("127.0.0.1", "localhost"),
                    new HostEntry("10.0.0.1", "api.test"),
                }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.DeleteConfirmation = _ => true;

        viewModel.DeleteEntry(viewModel.Groups[0].Entries[0]);

        viewModel.Groups.ShouldHaveSingleItem()
            .Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "api.test" });
    }

    [Fact]
    public void Successive_stagings_project_from_the_on_disk_baseline_without_double_applying()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        viewModel.AddEntry("10.0.0.1", "a.test");
        viewModel.AddEntry("10.0.0.2", "b.test");

        viewModel.Groups.ShouldHaveSingleItem()
            .Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "localhost", "a.test", "b.test" });
    }

    [Fact]
    public void A_status_flip_stays_visible_under_an_active_search_query()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.SearchQuery = "web";

        viewModel.DisableEntry(viewModel.Groups[0].Entries[0]);

        var visible = viewModel.VisibleGroups.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem();
        visible.Hostname.ShouldBe("web.test");
        visible.Status.ShouldBe(HostEntryStatus.Disabled);
    }

    [Fact]
    public void A_staged_addition_matching_the_active_query_appears_in_the_visible_groups()
    {
        var repository = new RecordingHostsFileRepository
        {
            GroupedEntries = new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }),
            },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.Load();
        viewModel.SearchQuery = "api";
        viewModel.VisibleGroups.ShouldBeEmpty();

        viewModel.AddEntry("10.0.0.1", "api.test");

        viewModel.VisibleGroups.ShouldHaveSingleItem()
            .Entries.ShouldHaveSingleItem().Hostname.ShouldBe("api.test");
    }

    [Fact]
    public void Staging_before_the_file_is_loaded_does_not_crash_and_leaves_the_view_at_the_baseline()
    {
        var viewModel = BuildViewModel(new RecordingHostsFileRepository());

        Should.NotThrow(() => viewModel.AddEntry("127.0.0.1", "a.test"));

        viewModel.PendingChangeCount.ShouldBe(1);
        viewModel.Groups.ShouldBeEmpty();
    }

    private static MainViewModel BuildViewModel(
        RecordingHostsFileRepository repository,
        bool isElevated = true,
        RecordingDnsCacheFlusher? dnsCacheFlusher = null,
        PendingChangeSet? pendingChanges = null) =>
        new(
            new ViewGroupedEntriesUseCase(repository),
            new ViewHostnameConflictsUseCase(repository),
            new ViewDuplicateEntriesUseCase(repository),
            new PreviewPendingChangesUseCase(repository),
            new ApplyPendingChangesUseCase(
                repository,
                new FakeElevationService(isElevated),
                dnsCacheFlusher ?? new RecordingDnsCacheFlusher()),
            new DiscardPendingChangesUseCase(repository),
            new SearchEntriesByHostnameUseCase(repository),
            new SearchEntriesByIpAddressUseCase(repository),
            new FilterEntriesByCategoryUseCase(repository),
            new SaveProfileUseCase(new StubProfileStore()),
            new RenameProfileUseCase(new StubProfileStore()),
            new RemoveProfileUseCase(new StubProfileStore()),
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

    private sealed class StubProfileStore : IProfileStore
    {
        public void Save(Profile profile)
        {
        }

        public Profile? Find(string name) => null;

        public IReadOnlyList<Profile> GetAll() => Array.Empty<Profile>();

        public void Remove(string name) => throw new NotSupportedException();
    }

    private sealed class FakeElevationService(bool isElevated) : IElevationService
    {
        public bool IsElevated { get; } = isElevated;
    }

    private sealed class RecordingDnsCacheFlusher : IDnsCacheFlusher
    {
        public int FlushCount { get; private set; }

        public void Flush() => FlushCount++;
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        private readonly List<string> _operations = new();

        public IReadOnlyList<CategoryGroup> GroupedEntries { get; set; } = Array.Empty<CategoryGroup>();

        public IReadOnlyList<HostEntry> ActiveEntries { get; set; } = Array.Empty<HostEntry>();

        public IReadOnlyList<string> Operations => _operations;

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => GroupedEntries;

        public IReadOnlyList<HostEntry> GetActiveEntries() => ActiveEntries;

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => _operations.Add($"Add {entry} cat={category ?? "<none>"}");

        public void CreateCategory(string name) => _operations.Add($"CreateCategory {name}");

        public void RenameCategory(string currentName, string newName)
            => _operations.Add($"RenameCategory {currentName} -> {newName}");

        public void DeleteCategory(string name) => _operations.Add($"DeleteCategory {name}");

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => _operations.Add($"Update {target} -> {replacement}");

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => _operations.Add($"Move {entry} to {targetCategory ?? "<none>"}");

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => _operations.Add($"Move {entry} to {targetCategory ?? "<none>"} at {targetIndex}");

        public void ReorderEntry(HostEntry entry, int targetIndex)
            => _operations.Add($"Reorder {entry} to {targetIndex}");

        public void DeleteEntry(HostEntry entry) => _operations.Add($"Delete {entry}");
    }
}
