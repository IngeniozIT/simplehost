using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.SearchEntriesByHostname;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class SearchEntriesByHostnameUseCaseTests
{
    [Fact]
    public void Narrows_to_the_entries_whose_hostname_matches_the_query()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("myapp").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.1", "api.myapp.test"),
        });
    }

    [Fact]
    public void Matches_a_partial_hostname_substring()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("example").ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Matches_case_insensitively()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "Web.MyApp.Test"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("myapp").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "Web.MyApp.Test"),
        });
    }

    [Fact]
    public void Matches_only_on_the_hostname_not_the_ip_or_comment()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("10.0.0.5", "web.test", "staging box"),
            new HostEntry("127.0.0.1", "api.test"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("staging").ShouldBeEmpty();
        useCase.Execute("10.0.0.5").ShouldBeEmpty();
    }

    [Fact]
    public void Includes_matching_disabled_entries_so_they_can_still_be_found()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("myapp").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Preserves_the_file_order_of_the_matching_entries()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("0.0.0.0", "z.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "a.myapp.test"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("myapp").Select(entry => entry.Hostname)
            .ShouldBe(new[] { "z.myapp.test", "a.myapp.test" });
    }

    [Fact]
    public void Returns_all_entries_when_the_query_is_empty_so_nothing_is_narrowed()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Returns_all_entries_when_the_query_is_only_whitespace()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("   ").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Ignores_whitespace_surrounding_the_query()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("  myapp  ").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
        });
    }

    [Fact]
    public void Returns_an_empty_list_when_no_hostname_matches_the_query()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new SearchEntriesByHostnameUseCase(repository);

        useCase.Execute("nomatch").ShouldBeEmpty();
    }

    [Fact]
    public void Returns_an_empty_list_when_there_are_no_entries()
    {
        var useCase = new SearchEntriesByHostnameUseCase(new FakeHostsFileRepository());

        useCase.Execute("myapp").ShouldBeEmpty();
    }

    private sealed class FakeHostsFileRepository(params HostEntry[] entries) : IHostsFileRepository
    {
        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries()
            => entries.Where(entry => entry.Status == HostEntryStatus.Active).ToList();

        public IReadOnlyList<HostEntry> GetEntries() => entries;

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();
        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();
        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
