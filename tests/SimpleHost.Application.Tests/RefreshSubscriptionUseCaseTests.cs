using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.RefreshSubscription;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class RefreshSubscriptionUseCaseTests
{
    [Fact]
    public void Replaces_the_categorys_entries_with_the_freshly_fetched_ones()
    {
        var old = new[]
        {
            new HostEntry("0.0.0.0", "old-ads.example.com"),
            new HostEntry("0.0.0.0", "old-tracker.example.com"),
        };
        var fresh = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
            new HostEntry("0.0.0.0", "beacon.example.com"),
        };
        var fetcher = new StubFetcher(fresh);
        var repository = new RecordingRepository(new CategoryGroup("Ad block", old));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        fetcher.RequestedUrl.ShouldBe("https://lists.example.com/adblock.txt");
        repository.Deleted.ShouldBe(old);
        repository.Added.ShouldBe(new[]
        {
            (fresh[0], (string?)"Ad block"),
            (fresh[1], (string?)"Ad block"),
            (fresh[2], (string?)"Ad block"),
        });
    }

    [Fact]
    public void Fetches_first_then_removes_the_old_entries_before_adding_the_new_ones()
    {
        var old = new[] { new HostEntry("0.0.0.0", "old.example.com") };
        var fresh = new[] { new HostEntry("0.0.0.0", "ads.example.com") };
        var fetcher = new StubFetcher(fresh);
        var repository = new RecordingRepository(new CategoryGroup("Ad block", old));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        repository.Log.ShouldBe(new[] { "delete:old.example.com", "add:ads.example.com:Ad block" });
    }

    [Fact]
    public void Preserves_each_fetched_entrys_status_and_inline_comment_and_order()
    {
        var fresh = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
            new HostEntry("0.0.0.0", "staging.example.com", Status: HostEntryStatus.Disabled),
            new HostEntry("127.0.0.1", "local.example.com"),
        };
        var fetcher = new StubFetcher(fresh);
        var repository = new RecordingRepository(
            new CategoryGroup("Ad block", new[] { new HostEntry("0.0.0.0", "old.example.com") }));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        repository.Added.Select(added => added.Entry).ShouldBe(fresh);
    }

    [Fact]
    public void Returns_the_freshly_fetched_entries()
    {
        var fresh = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
        };
        var repository = new RecordingRepository(
            new CategoryGroup("Ad block", new[] { new HostEntry("0.0.0.0", "old.example.com") }));
        var useCase = new RefreshSubscriptionUseCase(new StubFetcher(fresh), repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block").ShouldBe(fresh);
    }

    [Fact]
    public void Empties_the_category_when_the_remote_list_is_now_empty()
    {
        var old = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
        };
        var fetcher = new StubFetcher(Array.Empty<HostEntry>());
        var repository = new RecordingRepository(new CategoryGroup("Ad block", old));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/empty.txt", "Ad block").ShouldBeEmpty();

        repository.Deleted.ShouldBe(old);
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Adds_the_fetched_entries_when_the_category_was_already_empty()
    {
        var fresh = new[] { new HostEntry("0.0.0.0", "ads.example.com") };
        var fetcher = new StubFetcher(fresh);
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        repository.Deleted.ShouldBeEmpty();
        repository.Added.ShouldBe(new[] { (fresh[0], (string?)"Ad block") });
    }

    [Fact]
    public void Only_touches_the_named_subscription_category()
    {
        var adBlockOld = new[] { new HostEntry("0.0.0.0", "old-ads.example.com") };
        var otherEntries = new[] { new HostEntry("10.0.0.1", "intranet.example.com") };
        var fresh = new[] { new HostEntry("0.0.0.0", "ads.example.com") };
        var fetcher = new StubFetcher(fresh);
        var repository = new RecordingRepository(
            new CategoryGroup(null, Array.Empty<HostEntry>()),
            new CategoryGroup("Ad block", adBlockOld),
            new CategoryGroup("Work", otherEntries));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        repository.Deleted.ShouldBe(adBlockOld);
        repository.Added.ShouldBe(new[] { (fresh[0], (string?)"Ad block") });
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_category_name()
    {
        var fresh = new[] { new HostEntry("0.0.0.0", "ads.example.com") };
        var fetcher = new StubFetcher(fresh);
        var repository = new RecordingRepository(
            new CategoryGroup("Ad block", new[] { new HostEntry("0.0.0.0", "old.example.com") }));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "  Ad block  ");

        repository.Added.ShouldBe(new[] { (fresh[0], (string?)"Ad block") });
    }

    [Fact]
    public void Rejects_refreshing_a_category_that_does_not_exist_without_fetching_or_mutating()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(new CategoryGroup(null, Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        var exception = Should.Throw<InvalidOperationException>(
            () => useCase.Execute("https://lists.example.com/adblock.txt", "Ad block"));
        exception.Message.ShouldContain("Ad block");
        fetcher.WasCalled.ShouldBeFalse();
        repository.Deleted.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Matches_the_subscription_category_name_case_sensitively()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        Should.Throw<InvalidOperationException>(
            () => useCase.Execute("https://lists.example.com/adblock.txt", "ad block"));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Deleted.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Leaves_the_existing_entries_untouched_when_the_fetch_fails()
    {
        var fetcher = new ThrowingFetcher();
        var repository = new RecordingRepository(
            new CategoryGroup("Ad block", new[] { new HostEntry("0.0.0.0", "ads.example.com") }));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        Should.Throw<HttpRequestException>(
            () => useCase.Execute("https://lists.example.com/adblock.txt", "Ad block"));
        repository.Deleted.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_url_that_is_empty_or_whitespace(string url)
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(url, "Ad block"));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Deleted.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_url()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(null!, "Ad block"));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Deleted.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_category_name_that_is_empty_or_whitespace(string category)
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute("https://lists.example.com/adblock.txt", category));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Deleted.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_category_name()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new RefreshSubscriptionUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute("https://lists.example.com/adblock.txt", null!));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Deleted.ShouldBeEmpty();
    }

    private sealed class StubFetcher(IReadOnlyList<HostEntry> entries) : IRemoteHostsListFetcher
    {
        public bool WasCalled { get; private set; }

        public string? RequestedUrl { get; private set; }

        public IReadOnlyList<HostEntry> Fetch(string url)
        {
            WasCalled = true;
            RequestedUrl = url;
            return entries;
        }
    }

    private sealed class ThrowingFetcher : IRemoteHostsListFetcher
    {
        public IReadOnlyList<HostEntry> Fetch(string url) => throw new HttpRequestException("unreachable");
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
