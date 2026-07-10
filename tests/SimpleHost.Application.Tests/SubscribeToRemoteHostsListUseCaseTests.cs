using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.SubscribeToRemoteHostsList;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class SubscribeToRemoteHostsListUseCaseTests
{
    [Fact]
    public void Creates_the_dedicated_category_before_adding_the_fetched_entries_under_it()
    {
        var entries = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
        };
        var fetcher = new StubFetcher(entries);
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        fetcher.RequestedUrl.ShouldBe("https://lists.example.com/adblock.txt");
        repository.Created.ShouldBe(new[] { "Ad block" });
        repository.Added.ShouldBe(new[]
        {
            (entries[0], (string?)"Ad block"),
            (entries[1], (string?)"Ad block"),
        });
    }

    [Fact]
    public void Creates_the_category_before_it_adds_any_entry()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        repository.Log.ShouldBe(new[] { "create:Ad block", "add:ads.example.com:Ad block" });
    }

    [Fact]
    public void Preserves_each_fetched_entrys_status_and_inline_comment_and_order()
    {
        var entries = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
            new HostEntry("0.0.0.0", "staging.example.com", Status: HostEntryStatus.Disabled),
            new HostEntry("127.0.0.1", "local.example.com"),
        };
        var fetcher = new StubFetcher(entries);
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block");

        repository.Added.Select(added => added.Entry).ShouldBe(entries);
    }

    [Fact]
    public void Returns_the_fetched_entries()
    {
        var entries = new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
        };
        var useCase = new SubscribeToRemoteHostsListUseCase(new StubFetcher(entries), new RecordingRepository());

        useCase.Execute("https://lists.example.com/adblock.txt", "Ad block").ShouldBe(entries);
    }

    [Fact]
    public void Creates_an_empty_dedicated_category_when_the_remote_list_holds_no_entries()
    {
        var fetcher = new StubFetcher(Array.Empty<HostEntry>());
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/empty.txt", "Ad block").ShouldBeEmpty();

        repository.Created.ShouldBe(new[] { "Ad block" });
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_category_name()
    {
        var entries = new[] { new HostEntry("0.0.0.0", "ads.example.com") };
        var fetcher = new StubFetcher(entries);
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "  Ad block  ");

        repository.Created.ShouldBe(new[] { "Ad block" });
        repository.Added.ShouldBe(new[] { (entries[0], (string?)"Ad block") });
    }

    [Fact]
    public void Rejects_a_category_name_that_already_exists_without_fetching_or_creating()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(
            new CategoryGroup(null, Array.Empty<HostEntry>()),
            new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        var exception = Should.Throw<InvalidOperationException>(
            () => useCase.Execute("https://lists.example.com/adblock.txt", "Ad block"));
        exception.Message.ShouldContain("Ad block");
        fetcher.WasCalled.ShouldBeFalse();
        repository.Created.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_category_name_that_already_exists_after_trimming()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        Should.Throw<InvalidOperationException>(
            () => useCase.Execute("https://lists.example.com/adblock.txt", "  Ad block  "));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Created.ShouldBeEmpty();
    }

    [Fact]
    public void Matches_the_existing_category_name_case_sensitively()
    {
        var entries = new[] { new HostEntry("0.0.0.0", "ads.example.com") };
        var fetcher = new StubFetcher(entries);
        var repository = new RecordingRepository(new CategoryGroup("Ad block", Array.Empty<HostEntry>()));
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        useCase.Execute("https://lists.example.com/adblock.txt", "ad block");

        repository.Created.ShouldBe(new[] { "ad block" });
        repository.Added.ShouldBe(new[] { (entries[0], (string?)"ad block") });
    }

    [Fact]
    public void Leaves_no_category_behind_when_the_fetch_fails()
    {
        var fetcher = new ThrowingFetcher();
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        Should.Throw<HttpRequestException>(
            () => useCase.Execute("https://lists.example.com/adblock.txt", "Ad block"));
        repository.Created.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_url_that_is_empty_or_whitespace(string url)
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(url, "Ad block"));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Created.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_url()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(null!, "Ad block"));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Created.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_category_name_that_is_empty_or_whitespace(string category)
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute("https://lists.example.com/adblock.txt", category));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Created.ShouldBeEmpty();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_category_name()
    {
        var fetcher = new StubFetcher(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new RecordingRepository();
        var useCase = new SubscribeToRemoteHostsListUseCase(fetcher, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute("https://lists.example.com/adblock.txt", null!));
        fetcher.WasCalled.ShouldBeFalse();
        repository.Created.ShouldBeEmpty();
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
        private readonly List<string> _created = new();
        private readonly List<string> _log = new();

        public IReadOnlyList<(HostEntry Entry, string? Category)> Added => _added;

        public IReadOnlyList<string> Created => _created;

        public IReadOnlyList<string> Log => _log;

        public void AddEntry(HostEntry entry, string? category = null)
        {
            _added.Add((entry, category));
            _log.Add($"add:{entry.Hostname}:{category}");
        }

        public void CreateCategory(string name)
        {
            _created.Add(name);
            _log.Add($"create:{name}");
        }

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();
    }
}
