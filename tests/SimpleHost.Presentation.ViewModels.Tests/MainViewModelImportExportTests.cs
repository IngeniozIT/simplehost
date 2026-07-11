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
using SimpleHost.Domain.Subscriptions;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class MainViewModelImportExportTests
{
    [Fact]
    public void Exporting_a_category_writes_its_entries_to_the_chosen_file()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Development", new HostEntry("10.0.0.1", "api.test"));
        var exporter = new RecordingHostsFileExporter();
        var viewModel = BuildViewModel(repository, exporter: exporter);
        viewModel.Load();

        viewModel.ExportCategory("Development", "/tmp/dev.hosts");

        exporter.ExportedCategory.ShouldNotBeNull();
        exporter.ExportedCategory!.Name.ShouldBe("Development");
        exporter.ExportedCategory.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "api.test" });
        exporter.ExportedCategoryPath.ShouldBe("/tmp/dev.hosts");
    }

    [Fact]
    public void Exporting_the_no_category_group_selects_the_uncategorized_entries()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var exporter = new RecordingHostsFileExporter();
        var viewModel = BuildViewModel(repository, exporter: exporter);
        viewModel.Load();

        viewModel.ExportCategory(null, "/tmp/none.hosts");

        exporter.ExportedCategory.ShouldNotBeNull();
        exporter.ExportedCategory!.Name.ShouldBeNull();
        exporter.ExportedCategory.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "localhost" });
    }

    [Fact]
    public void Exporting_a_missing_category_reports_the_failure()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var exporter = new RecordingHostsFileExporter();
        var viewModel = BuildViewModel(repository, exporter: exporter);
        viewModel.Load();
        string? reported = null;
        viewModel.ImportExportFailed = message => reported = message;

        viewModel.ExportCategory("Missing", "/tmp/missing.hosts");

        reported.ShouldNotBeNull();
        exporter.ExportedCategory.ShouldBeNull();
    }

    [Fact]
    public void Exporting_a_category_with_a_blank_destination_reports_the_failure()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Development", new HostEntry("10.0.0.1", "api.test"));
        var exporter = new RecordingHostsFileExporter();
        var viewModel = BuildViewModel(repository, exporter: exporter);
        viewModel.Load();
        string? reported = null;
        viewModel.ImportExportFailed = message => reported = message;

        viewModel.ExportCategory("Development", "   ");

        reported.ShouldNotBeNull();
        exporter.ExportedCategory.ShouldBeNull();
    }

    [Fact]
    public void Exporting_the_whole_configuration_writes_every_category_to_the_chosen_file()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        repository.Seed("Development", new HostEntry("10.0.0.1", "api.test"));
        var exporter = new RecordingHostsFileExporter();
        var viewModel = BuildViewModel(repository, exporter: exporter);
        viewModel.Load();

        viewModel.ExportConfiguration("/tmp/all.hosts");

        exporter.ExportedConfiguration.ShouldNotBeNull();
        exporter.ExportedConfiguration!.Select(group => group.Name).ShouldBe(new string?[] { null, "Development" });
        exporter.ExportedConfigurationPath.ShouldBe("/tmp/all.hosts");
    }

    [Fact]
    public void Exporting_the_configuration_with_a_blank_destination_reports_the_failure()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Development", new HostEntry("10.0.0.1", "api.test"));
        var exporter = new RecordingHostsFileExporter();
        var viewModel = BuildViewModel(repository, exporter: exporter);
        viewModel.Load();
        string? reported = null;
        viewModel.ImportExportFailed = message => reported = message;

        viewModel.ExportConfiguration("   ");

        reported.ShouldNotBeNull();
        exporter.ExportedConfiguration.ShouldBeNull();
    }

    [Fact]
    public void Importing_entries_adds_them_under_the_chosen_category_and_shows_them()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Development", new HostEntry("10.0.0.1", "existing.test"));
        var importer = new FakeHostsFileImporter();
        importer.Serve(
            "/tmp/extra.hosts",
            new HostEntry("10.0.0.2", "imported-one.test"),
            new HostEntry("10.0.0.3", "imported-two.test"));
        var viewModel = BuildViewModel(repository, importer: importer);
        viewModel.Load();

        viewModel.ImportEntries("/tmp/extra.hosts", "Development");

        var group = viewModel.Groups.Single(candidate => candidate.Name == "Development");
        group.Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "existing.test", "imported-one.test", "imported-two.test" });
    }

    [Fact]
    public void Importing_into_the_no_category_group_adds_the_entries_there()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var importer = new FakeHostsFileImporter();
        importer.Serve("/tmp/extra.hosts", new HostEntry("0.0.0.0", "blocked.test"));
        var viewModel = BuildViewModel(repository, importer: importer);
        viewModel.Load();

        viewModel.ImportEntries("/tmp/extra.hosts", null);

        var group = viewModel.Groups.Single(candidate => candidate.Name is null);
        group.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "localhost", "blocked.test" });
    }

    [Fact]
    public void Importing_from_a_blank_source_reports_the_failure_and_changes_nothing()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var viewModel = BuildViewModel(repository, importer: new FakeHostsFileImporter());
        viewModel.Load();
        string? reported = null;
        viewModel.ImportExportFailed = message => reported = message;

        viewModel.ImportEntries("   ", null);

        reported.ShouldNotBeNull();
        viewModel.Groups.Single(candidate => candidate.Name is null)
            .Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "localhost" });
    }

    [Fact]
    public void Importing_into_a_missing_category_reports_the_failure_and_changes_nothing()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var importer = new FakeHostsFileImporter();
        importer.Serve("/tmp/extra.hosts", new HostEntry("0.0.0.0", "blocked.test"));
        var viewModel = BuildViewModel(repository, importer: importer);
        viewModel.Load();
        string? reported = null;
        viewModel.ImportExportFailed = message => reported = message;

        viewModel.ImportEntries("/tmp/extra.hosts", "Missing");

        reported.ShouldNotBeNull();
        viewModel.Groups.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    [Fact]
    public void A_failed_import_leaves_the_displayed_state_untouched_without_re_reading()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Existing", new HostEntry("0.0.0.0", "kept.test"));
        var viewModel = BuildViewModel(repository, importer: new FakeHostsFileImporter());
        viewModel.Load();

        // An external change lands after load; it stays invisible until the view re-reads the file.
        repository.Seed("Added later", new HostEntry("0.0.0.0", "unseen.test"));

        // A refused import (blank source) must leave the displayed state exactly as it was — it must
        // not re-read the file, so the category added afterwards must not appear.
        viewModel.ImportEntries("   ", "Existing");

        viewModel.Groups.Select(group => group.Name).ShouldBe(new[] { "Existing" });
    }

    [Fact]
    public void A_failed_import_or_export_without_a_handler_does_not_throw()
    {
        var repository = new StatefulHostsFileRepository();
        var viewModel = BuildViewModel(repository);
        viewModel.Load();

        Should.NotThrow(() =>
        {
            viewModel.ExportCategory("Missing", "/tmp/x.hosts");
            viewModel.ExportConfiguration("   ");
            viewModel.ImportEntries("   ", null);
        });
    }

    private static MainViewModel BuildViewModel(
        StatefulHostsFileRepository repository,
        RecordingHostsFileExporter? exporter = null,
        FakeHostsFileImporter? importer = null) =>
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
            new SaveProfileUseCase(new FakeProfileStore()),
            new RenameProfileUseCase(new FakeProfileStore()),
            new RemoveProfileUseCase(new FakeProfileStore()),
            new SubscribeToRemoteHostsListUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionsUseCase(new StubSubscriptionCatalog(), new StubRemoteHostsListFetcher(), repository),
            new ExportCategoryUseCase(repository, exporter ?? new RecordingHostsFileExporter()),
            new ExportConfigurationUseCase(repository, exporter ?? new RecordingHostsFileExporter()),
            new ImportHostsFileUseCase(importer ?? new FakeHostsFileImporter(), repository),
            new PendingChangeSet());

    private sealed class RecordingHostsFileExporter : IHostsFileExporter
    {
        public CategoryGroup? ExportedCategory { get; private set; }

        public string? ExportedCategoryPath { get; private set; }

        public IReadOnlyList<CategoryGroup>? ExportedConfiguration { get; private set; }

        public string? ExportedConfigurationPath { get; private set; }

        public void ExportCategory(CategoryGroup group, string destinationPath)
        {
            ExportedCategory = group;
            ExportedCategoryPath = destinationPath;
        }

        public void ExportConfiguration(IReadOnlyList<CategoryGroup> groups, string destinationPath)
        {
            ExportedConfiguration = groups;
            ExportedConfigurationPath = destinationPath;
        }
    }

    private sealed class FakeHostsFileImporter : IHostsFileImporter
    {
        private readonly Dictionary<string, IReadOnlyList<HostEntry>> _files = new();

        public void Serve(string sourcePath, params HostEntry[] entries) => _files[sourcePath] = entries;

        public IReadOnlyList<HostEntry> ImportEntries(string sourcePath)
            => _files.TryGetValue(sourcePath, out var entries)
                ? entries
                : throw new InvalidOperationException($"The file '{sourcePath}' could not be read.");
    }

    private sealed class StubRemoteHostsListFetcher : IRemoteHostsListFetcher
    {
        public IReadOnlyList<HostEntry> Fetch(string url) => throw new NotSupportedException();
    }

    private sealed class StubSubscriptionCatalog : ISubscriptionCatalog
    {
        public IReadOnlyList<Subscription> GetSubscriptions() => Array.Empty<Subscription>();
    }

    private sealed class FakeProfileStore : IProfileStore
    {
        private readonly List<Domain.Profiles.Profile> _saved = new();

        public void Save(Domain.Profiles.Profile profile)
        {
            _saved.RemoveAll(existing => existing.Name == profile.Name);
            _saved.Add(profile);
        }

        public Domain.Profiles.Profile? Find(string name)
            => _saved.FirstOrDefault(profile => profile.Name == name);

        public IReadOnlyList<Domain.Profiles.Profile> GetAll() => _saved.ToList();

        public void Remove(string name) => _saved.RemoveAll(existing => existing.Name == name);
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

    private sealed class StatefulHostsFileRepository : IHostsFileRepository
    {
        private readonly List<MutableGroup> _groups = new();

        public void Seed(string? category, params HostEntry[] entries)
        {
            var group = GroupFor(category, create: true)!;
            foreach (var entry in entries)
            {
                group.Entries.Add(entry);
            }
        }

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => _groups.Select(group => new CategoryGroup(group.Name, group.Entries.ToList())).ToList();

        public IReadOnlyList<HostEntry> GetEntries()
            => _groups.SelectMany(group => group.Entries).ToList();

        public IReadOnlyList<HostEntry> GetActiveEntries()
            => _groups.SelectMany(group => group.Entries)
                .Where(entry => entry.Status == HostEntryStatus.Active)
                .ToList();

        public void CreateCategory(string name)
        {
            if (_groups.Any(group => group.Name == name))
            {
                throw new InvalidOperationException($"Category '{name}' already exists.");
            }

            _groups.Add(new MutableGroup(name));
        }

        public void AddEntry(HostEntry entry, string? category = null)
            => GroupFor(category, create: true)!.Entries.Add(entry);

        public void DeleteEntry(HostEntry entry)
        {
            foreach (var group in _groups)
            {
                group.Entries.Remove(entry);
            }
        }

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        private MutableGroup? GroupFor(string? category, bool create)
        {
            var group = _groups.FirstOrDefault(candidate => candidate.Name == category);
            if (group is null && create)
            {
                group = new MutableGroup(category);
                _groups.Add(group);
            }

            return group;
        }

        private sealed class MutableGroup(string? name)
        {
            public string? Name { get; } = name;

            public List<HostEntry> Entries { get; } = new();
        }
    }
}
