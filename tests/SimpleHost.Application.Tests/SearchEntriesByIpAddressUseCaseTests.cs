using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.SearchEntriesByIpAddress;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class SearchEntriesByIpAddressUseCaseTests
{
    [Fact]
    public void Narrows_to_the_entries_whose_ip_address_matches_the_query()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("192.168.1.10", "web.myapp.test"),
            new HostEntry("192.168.1.20", "api.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("192.168").ShouldBe(new[]
        {
            new HostEntry("192.168.1.10", "web.myapp.test"),
            new HostEntry("192.168.1.20", "api.myapp.test"),
        });
    }

    [Fact]
    public void Matches_a_partial_ip_address_substring()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("10.0.0").ShouldBe(new[]
        {
            new HostEntry("10.0.0.5", "ads.example.com"),
        });
    }

    [Fact]
    public void Matches_case_insensitively_so_ipv6_hex_is_found_regardless_of_casing()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("FE80::1A2B", "web.myapp.test"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("fe80::1a2b").ShouldBe(new[]
        {
            new HostEntry("FE80::1A2B", "web.myapp.test"),
        });
    }

    [Fact]
    public void Matches_only_on_the_ip_address_not_the_hostname_or_comment()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("10.0.0.5", "web.test", "staging box"),
            new HostEntry("127.0.0.1", "api.test"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("web").ShouldBeEmpty();
        useCase.Execute("staging").ShouldBeEmpty();
    }

    [Fact]
    public void Includes_matching_disabled_entries_so_they_can_still_be_found()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("192.168.1.10", "web.myapp.test"),
            new HostEntry("192.168.1.20", "staging.myapp.test", Status: HostEntryStatus.Disabled));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("192.168").ShouldBe(new[]
        {
            new HostEntry("192.168.1.10", "web.myapp.test"),
            new HostEntry("192.168.1.20", "staging.myapp.test", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Preserves_the_file_order_of_the_matching_entries()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("10.0.0.9", "z.myapp.test"),
            new HostEntry("192.168.1.1", "ads.example.com"),
            new HostEntry("10.0.0.3", "a.myapp.test"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("10.0.0").Select(entry => entry.Hostname)
            .ShouldBe(new[] { "z.myapp.test", "a.myapp.test" });
    }

    [Fact]
    public void Returns_all_entries_when_the_query_is_empty_so_nothing_is_narrowed()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"),
        });
    }

    [Fact]
    public void Returns_all_entries_when_the_query_is_only_whitespace()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("   ").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"),
        });
    }

    [Fact]
    public void Ignores_whitespace_surrounding_the_query()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("  127.0.0.1  ").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
        });
    }

    [Fact]
    public void Returns_an_empty_list_when_no_ip_address_matches_the_query()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("10.0.0.5", "ads.example.com"));
        var useCase = new SearchEntriesByIpAddressUseCase(repository);

        useCase.Execute("172.16").ShouldBeEmpty();
    }

    [Fact]
    public void Returns_an_empty_list_when_there_are_no_entries()
    {
        var useCase = new SearchEntriesByIpAddressUseCase(new FakeHostsFileRepository());

        useCase.Execute("127.0.0.1").ShouldBeEmpty();
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
