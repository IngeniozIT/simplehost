using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ApplyPendingChanges;
using SimpleHost.Application.UseCases.DiscardPendingChanges;
using SimpleHost.Application.UseCases.FilterEntriesByCategory;
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
using SimpleHost.Domain.Subscriptions;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class MainViewModelSubscriptionsTests
{
    [Fact]
    public void Subscribing_to_a_remote_list_shows_its_entries_in_a_new_category()
    {
        var repository = new StatefulHostsFileRepository();
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve(
            "https://ads.example/hosts",
            new HostEntry("0.0.0.0", "ads.test"),
            new HostEntry("0.0.0.0", "tracker.test"));
        var viewModel = BuildViewModel(repository, fetcher);
        viewModel.Load();

        viewModel.SubscribeToRemoteList("https://ads.example/hosts", "Ad blocking");

        var group = viewModel.Groups.ShouldHaveSingleItem();
        group.Name.ShouldBe("Ad blocking");
        group.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "ads.test", "tracker.test" });
    }

    [Fact]
    public void Subscribing_places_the_fetched_entries_under_the_chosen_category_in_list_order()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve(
            "https://list.example/hosts",
            new HostEntry("0.0.0.0", "first.test"),
            new HostEntry("0.0.0.0", "second.test", null, HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "third.test"));
        var viewModel = BuildViewModel(repository, fetcher);
        viewModel.Load();

        viewModel.SubscribeToRemoteList("https://list.example/hosts", "Blocklist");

        var group = viewModel.Groups.Single(candidate => candidate.Name == "Blocklist");
        group.Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "first.test", "second.test", "third.test" });
        group.Entries[1].Status.ShouldBe(HostEntryStatus.Disabled);
    }

    [Fact]
    public void Subscribing_with_a_blank_url_changes_nothing_and_reports_the_failure()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var viewModel = BuildViewModel(repository, new FakeRemoteHostsListFetcher());
        viewModel.Load();
        string? reported = null;
        viewModel.SubscriptionOperationFailed = message => reported = message;

        viewModel.SubscribeToRemoteList("   ", "Blocklist");

        reported.ShouldNotBeNull();
        viewModel.Groups.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    [Fact]
    public void Subscribing_to_an_already_existing_category_reports_the_failure_and_changes_nothing()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Blocklist", new HostEntry("0.0.0.0", "kept.test"));
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve("https://list.example/hosts", new HostEntry("0.0.0.0", "new.test"));
        var viewModel = BuildViewModel(repository, fetcher);
        viewModel.Load();
        string? reported = null;
        viewModel.SubscriptionOperationFailed = message => reported = message;

        viewModel.SubscribeToRemoteList("https://list.example/hosts", "Blocklist");

        reported.ShouldNotBeNull();
        var group = viewModel.Groups.Single(candidate => candidate.Name == "Blocklist");
        group.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "kept.test" });
    }

    [Fact]
    public void Subscribing_reports_a_failure_when_the_remote_list_cannot_be_fetched()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var fetcher = new FakeRemoteHostsListFetcher(); // serves nothing -> throws on fetch
        var viewModel = BuildViewModel(repository, fetcher);
        viewModel.Load();
        string? reported = null;
        viewModel.SubscriptionOperationFailed = message => reported = message;

        viewModel.SubscribeToRemoteList("https://unreachable.example/hosts", "Blocklist");

        reported.ShouldNotBeNull();
        viewModel.Groups.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    [Fact]
    public void Subscribing_failure_without_a_handler_does_not_throw()
    {
        var repository = new StatefulHostsFileRepository();
        var viewModel = BuildViewModel(repository, new FakeRemoteHostsListFetcher());
        viewModel.Load();

        Should.NotThrow(() => viewModel.SubscribeToRemoteList("   ", "Blocklist"));
    }

    [Fact]
    public void A_refused_subscription_operation_leaves_the_displayed_state_untouched_without_re_reading()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Existing", new HostEntry("0.0.0.0", "kept.test"));
        var viewModel = BuildViewModel(repository, new FakeRemoteHostsListFetcher());
        viewModel.Load();

        // An external change lands after load; it stays invisible until the view re-reads the file.
        repository.Seed("Added later", new HostEntry("0.0.0.0", "unseen.test"));

        // A refused operation (blank URL) must leave the displayed state exactly as it was — it must
        // not re-read the file, so the category added afterwards must not appear.
        viewModel.SubscribeToRemoteList("   ", "Whatever");

        viewModel.Groups.Select(group => group.Name).ShouldBe(new[] { "Existing" });
    }

    [Fact]
    public void Refreshing_a_subscription_replaces_its_category_with_the_latest_fetched_entries()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(
            "Blocklist",
            new HostEntry("0.0.0.0", "old-one.test"),
            new HostEntry("0.0.0.0", "old-two.test"));
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve(
            "https://list.example/hosts",
            new HostEntry("0.0.0.0", "fresh-one.test"),
            new HostEntry("0.0.0.0", "fresh-two.test"),
            new HostEntry("0.0.0.0", "fresh-three.test"));
        var viewModel = BuildViewModel(repository, fetcher);
        viewModel.Load();

        viewModel.RefreshSubscription("https://list.example/hosts", "Blocklist");

        var group = viewModel.Groups.Single(candidate => candidate.Name == "Blocklist");
        group.Entries.Select(entry => entry.Hostname)
            .ShouldBe(new[] { "fresh-one.test", "fresh-two.test", "fresh-three.test" });
    }

    [Fact]
    public void Refreshing_a_missing_subscription_reports_the_failure_and_changes_nothing()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve("https://list.example/hosts", new HostEntry("0.0.0.0", "new.test"));
        var viewModel = BuildViewModel(repository, fetcher);
        viewModel.Load();
        string? reported = null;
        viewModel.SubscriptionOperationFailed = message => reported = message;

        viewModel.RefreshSubscription("https://list.example/hosts", "Missing");

        reported.ShouldNotBeNull();
        viewModel.Groups.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    [Fact]
    public void Refreshing_failure_without_a_handler_does_not_throw()
    {
        var repository = new StatefulHostsFileRepository();
        var viewModel = BuildViewModel(repository, new FakeRemoteHostsListFetcher());
        viewModel.Load();

        Should.NotThrow(() => viewModel.RefreshSubscription("https://list.example/hosts", "Missing"));
    }

    [Fact]
    public void Auto_refresh_reflects_the_outcome_of_each_subscription_in_catalog_order()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Ads", new HostEntry("0.0.0.0", "old-ads.test"));
        // "Trackers" is subscribed but its category was removed, so its refresh fails.
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve("https://ads.example/hosts", new HostEntry("0.0.0.0", "fresh-ads.test"));
        fetcher.Serve("https://trackers.example/hosts", new HostEntry("0.0.0.0", "fresh-trk.test"));
        var catalog = new FakeSubscriptionCatalog(
            Subscription.Of("https://ads.example/hosts", "Ads"),
            Subscription.Of("https://trackers.example/hosts", "Trackers"));
        var viewModel = BuildViewModel(repository, fetcher, catalog);
        viewModel.Load();

        viewModel.RefreshAllSubscriptions();

        viewModel.SubscriptionRefreshResults.Count.ShouldBe(2);
        viewModel.SubscriptionRefreshResults[0].Subscription.Category.ShouldBe("Ads");
        viewModel.SubscriptionRefreshResults[0].Succeeded.ShouldBeTrue();
        viewModel.SubscriptionRefreshResults[1].Subscription.Category.ShouldBe("Trackers");
        viewModel.SubscriptionRefreshResults[1].Succeeded.ShouldBeFalse();
        viewModel.SubscriptionRefreshResults[1].Error.ShouldNotBeNull();
    }

    [Fact]
    public void Auto_refresh_updates_the_grouped_view_with_the_refreshed_entries()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Ads", new HostEntry("0.0.0.0", "old-ads.test"));
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve("https://ads.example/hosts", new HostEntry("0.0.0.0", "fresh-ads.test"));
        var catalog = new FakeSubscriptionCatalog(Subscription.Of("https://ads.example/hosts", "Ads"));
        var viewModel = BuildViewModel(repository, fetcher, catalog);
        viewModel.Load();

        viewModel.RefreshAllSubscriptions();

        var group = viewModel.Groups.Single(candidate => candidate.Name == "Ads");
        group.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "fresh-ads.test" });
    }

    [Fact]
    public void Auto_refresh_replaces_the_previous_results_rather_than_appending()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed("Ads", new HostEntry("0.0.0.0", "old-ads.test"));
        var fetcher = new FakeRemoteHostsListFetcher();
        fetcher.Serve("https://ads.example/hosts", new HostEntry("0.0.0.0", "fresh-ads.test"));
        var catalog = new FakeSubscriptionCatalog(Subscription.Of("https://ads.example/hosts", "Ads"));
        var viewModel = BuildViewModel(repository, fetcher, catalog);
        viewModel.Load();

        viewModel.RefreshAllSubscriptions();
        viewModel.RefreshAllSubscriptions();

        viewModel.SubscriptionRefreshResults.Count.ShouldBe(1);
    }

    [Fact]
    public void Auto_refresh_with_no_subscriptions_reflects_no_results()
    {
        var repository = new StatefulHostsFileRepository();
        repository.Seed(null, new HostEntry("127.0.0.1", "localhost"));
        var viewModel = BuildViewModel(repository, new FakeRemoteHostsListFetcher(), new FakeSubscriptionCatalog());
        viewModel.Load();

        viewModel.RefreshAllSubscriptions();

        viewModel.SubscriptionRefreshResults.ShouldBeEmpty();
        viewModel.Groups.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    private static MainViewModel BuildViewModel(
        StatefulHostsFileRepository repository,
        FakeRemoteHostsListFetcher fetcher,
        FakeSubscriptionCatalog? catalog = null) =>
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
            new SubscribeToRemoteHostsListUseCase(fetcher, repository),
            new RefreshSubscriptionUseCase(fetcher, repository),
            new RefreshSubscriptionsUseCase(catalog ?? new FakeSubscriptionCatalog(), fetcher, repository),
            new PendingChangeSet());

    private sealed class FakeRemoteHostsListFetcher : IRemoteHostsListFetcher
    {
        private readonly Dictionary<string, IReadOnlyList<HostEntry>> _lists = new();

        public void Serve(string url, params HostEntry[] entries) => _lists[url] = entries;

        public IReadOnlyList<HostEntry> Fetch(string url)
            => _lists.TryGetValue(url, out var entries)
                ? entries
                : throw new InvalidOperationException($"The remote list at '{url}' is unreachable.");
    }

    private sealed class FakeSubscriptionCatalog : ISubscriptionCatalog
    {
        private readonly IReadOnlyList<Subscription> _subscriptions;

        public FakeSubscriptionCatalog(params Subscription[] subscriptions) => _subscriptions = subscriptions;

        public IReadOnlyList<Subscription> GetSubscriptions() => _subscriptions;
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

    // A stateful in-memory repository: unlike the throw-on-mutate fake used by the pending-change
    // tests, the subscription use cases write straight to the repository, so these tests need a
    // repository that actually records category creations, entry additions and deletions and reads
    // them back through the grouped view.
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
