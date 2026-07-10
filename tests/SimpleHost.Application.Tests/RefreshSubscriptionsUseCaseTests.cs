using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.RefreshSubscriptions;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Subscriptions;

namespace SimpleHost.Application.Tests;

public class RefreshSubscriptionsUseCaseTests
{
    private const string AdBlockUrl = "https://lists.example.com/adblock.txt";
    private const string MalwareUrl = "https://lists.example.com/malware.txt";

    [Fact]
    public void Refreshes_nothing_and_returns_no_results_when_there_are_no_subscriptions()
    {
        var fetcher = new StubFetcher();
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionsUseCase(new StubCatalog(), fetcher, repository);

        useCase.Execute().ShouldBeEmpty();

        fetcher.RequestedUrls.ShouldBeEmpty();
        repository.Deleted.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Refreshes_each_subscriptions_own_category_with_its_freshly_fetched_entries()
    {
        var adBlockOld = new[] { new HostEntry("0.0.0.0", "old-ads.example.com") };
        var malwareOld = new[] { new HostEntry("0.0.0.0", "old-malware.example.com") };
        var untouched = new[] { new HostEntry("10.0.0.1", "intranet.example.com") };
        var adBlockFresh = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
        };
        var malwareFresh = new[] { new HostEntry("0.0.0.0", "malware.example.com") };

        var fetcher = new StubFetcher()
            .Returning(AdBlockUrl, adBlockFresh)
            .Returning(MalwareUrl, malwareFresh);
        var repository = new RecordingRepository(
            new CategoryGroup("Ad block", adBlockOld),
            new CategoryGroup("Malware", malwareOld),
            new CategoryGroup("Work", untouched));
        var catalog = new StubCatalog(
            Subscription.Of(AdBlockUrl, "Ad block"),
            Subscription.Of(MalwareUrl, "Malware"));
        var useCase = new RefreshSubscriptionsUseCase(catalog, fetcher, repository);

        var results = useCase.Execute();

        fetcher.RequestedUrls.ShouldBe(new[] { AdBlockUrl, MalwareUrl });
        repository.Deleted.ShouldBe(new[] { adBlockOld[0], malwareOld[0] });
        repository.Added.ShouldBe(new[]
        {
            (adBlockFresh[0], (string?)"Ad block"),
            (adBlockFresh[1], (string?)"Ad block"),
            (malwareFresh[0], (string?)"Malware"),
        });

        results.Select(result => result.Subscription).ShouldBe(catalog.GetSubscriptions());
        results.ShouldAllBe(result => result.Succeeded && result.Error == null);
        results[0].Entries.ShouldBe(adBlockFresh);
        results[1].Entries.ShouldBe(malwareFresh);
    }

    [Fact]
    public void Fetches_each_remote_list_before_it_touches_that_categorys_entries()
    {
        var fetcher = new StubFetcher()
            .Returning(AdBlockUrl, new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(
            new CategoryGroup("Ad block", new[] { new HostEntry("0.0.0.0", "old.example.com") }));
        var catalog = new StubCatalog(Subscription.Of(AdBlockUrl, "Ad block"));
        var useCase = new RefreshSubscriptionsUseCase(catalog, fetcher, repository);

        useCase.Execute();

        repository.Log.ShouldBe(new[] { "delete:old.example.com", "add:ads.example.com:Ad block" });
    }

    [Fact]
    public void Leaves_a_categorys_entries_in_place_when_its_fetch_fails_and_still_refreshes_the_rest()
    {
        var adBlockOld = new[] { new HostEntry("0.0.0.0", "old-ads.example.com") };
        var malwareFresh = new[] { new HostEntry("0.0.0.0", "malware.example.com") };
        var fetcher = new StubFetcher()
            .Failing(AdBlockUrl, "adblock list unreachable")
            .Returning(MalwareUrl, malwareFresh);
        var repository = new RecordingRepository(
            new CategoryGroup("Ad block", adBlockOld),
            new CategoryGroup("Malware", new[] { new HostEntry("0.0.0.0", "old-malware.example.com") }));
        var catalog = new StubCatalog(
            Subscription.Of(AdBlockUrl, "Ad block"),
            Subscription.Of(MalwareUrl, "Malware"));
        var useCase = new RefreshSubscriptionsUseCase(catalog, fetcher, repository);

        var results = useCase.Execute();

        results[0].Succeeded.ShouldBeFalse();
        results[0].Error.ShouldNotBeNull().ShouldContain("adblock list unreachable");
        results[0].Entries.ShouldBeEmpty();
        results[1].Succeeded.ShouldBeTrue();
        results[1].Entries.ShouldBe(malwareFresh);

        repository.Added.ShouldBe(new[] { (malwareFresh[0], (string?)"Malware") });
        repository.Deleted.ShouldNotContain(adBlockOld[0]);
    }

    [Fact]
    public void Skips_a_subscription_whose_category_no_longer_exists_without_fetching_and_refreshes_the_rest()
    {
        var malwareFresh = new[] { new HostEntry("0.0.0.0", "malware.example.com") };
        var fetcher = new StubFetcher()
            .Returning("https://lists.example.com/gone.txt", new[] { new HostEntry("0.0.0.0", "x.example.com") })
            .Returning(MalwareUrl, malwareFresh);
        var repository = new RecordingRepository(
            new CategoryGroup("Malware", new[] { new HostEntry("0.0.0.0", "old-malware.example.com") }));
        var catalog = new StubCatalog(
            Subscription.Of("https://lists.example.com/gone.txt", "Gone"),
            Subscription.Of(MalwareUrl, "Malware"));
        var useCase = new RefreshSubscriptionsUseCase(catalog, fetcher, repository);

        var results = useCase.Execute();

        results[0].Succeeded.ShouldBeFalse();
        results[0].Error.ShouldNotBeNull().ShouldContain("Gone");
        fetcher.RequestedUrls.ShouldNotContain("https://lists.example.com/gone.txt");
        results[1].Succeeded.ShouldBeTrue();
        repository.Added.ShouldBe(new[] { (malwareFresh[0], (string?)"Malware") });
    }

    [Fact]
    public void Empties_a_category_when_its_remote_list_is_now_empty()
    {
        var old = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
        };
        var fetcher = new StubFetcher().Returning(AdBlockUrl, Array.Empty<HostEntry>());
        var repository = new RecordingRepository(new CategoryGroup("Ad block", old));
        var catalog = new StubCatalog(Subscription.Of(AdBlockUrl, "Ad block"));
        var useCase = new RefreshSubscriptionsUseCase(catalog, fetcher, repository);

        var results = useCase.Execute();

        results[0].Succeeded.ShouldBeTrue();
        results[0].Entries.ShouldBeEmpty();
        repository.Deleted.ShouldBe(old);
        repository.Added.ShouldBeEmpty();
    }

    private sealed class StubCatalog(params Subscription[] subscriptions) : ISubscriptionCatalog
    {
        public IReadOnlyList<Subscription> GetSubscriptions() => subscriptions;
    }

    private sealed class StubFetcher : IRemoteHostsListFetcher
    {
        private readonly Dictionary<string, IReadOnlyList<HostEntry>> _entriesByUrl = new();
        private readonly Dictionary<string, string> _failuresByUrl = new();

        public List<string> RequestedUrls { get; } = new();

        public StubFetcher Returning(string url, IReadOnlyList<HostEntry> entries)
        {
            _entriesByUrl[url] = entries;
            return this;
        }

        public StubFetcher Failing(string url, string reason)
        {
            _failuresByUrl[url] = reason;
            return this;
        }

        public IReadOnlyList<HostEntry> Fetch(string url)
        {
            RequestedUrls.Add(url);
            if (_failuresByUrl.TryGetValue(url, out var reason))
            {
                throw new HttpRequestException(reason);
            }

            return _entriesByUrl[url];
        }
    }

    private sealed class RecordingRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        private readonly List<(HostEntry Entry, string? Category)> _added = new();
        private readonly List<HostEntry> _deleted = new();
        private readonly List<string> _log = new();

        public IReadOnlyList<(HostEntry Entry, string? Category)> Added => _added;

        public IReadOnlyList<HostEntry> Deleted => _deleted;

        public IReadOnlyList<string> Log => _log;

        public void AddEntry(HostEntry entry, string? category = null)
        {
            _added.Add((entry, category));
            _log.Add($"add:{entry.Hostname}:{category}");
        }

        public void DeleteEntry(HostEntry entry)
        {
            _deleted.Add(entry);
            _log.Add($"delete:{entry.Hostname}");
        }

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();
    }
}
