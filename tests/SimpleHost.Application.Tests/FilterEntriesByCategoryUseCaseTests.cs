using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.FilterEntriesByCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class FilterEntriesByCategoryUseCaseTests
{
    [Fact]
    public void Narrows_to_the_entries_belonging_to_the_chosen_category()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test"),
                new HostEntry("127.0.0.1", "web.test"),
            }),
            new CategoryGroup("Blocklist", new[] { new HostEntry("0.0.0.0", "ads.example.com") }));
        var useCase = new FilterEntriesByCategoryUseCase(repository);

        useCase.Execute("Development").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
        });
    }

    [Fact]
    public void Selects_the_no_category_group_when_the_chosen_category_is_null()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var useCase = new FilterEntriesByCategoryUseCase(repository);

        useCase.Execute(null).ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
        });
    }

    [Fact]
    public void Includes_disabled_entries_of_the_chosen_category()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test"),
                new HostEntry("127.0.0.1", "staging.test", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new FilterEntriesByCategoryUseCase(repository);

        useCase.Execute("Development").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "staging.test", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Preserves_the_file_order_of_the_entries_in_the_category()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("0.0.0.0", "z.test"),
                new HostEntry("0.0.0.0", "a.test"),
                new HostEntry("0.0.0.0", "m.test"),
            }));
        var useCase = new FilterEntriesByCategoryUseCase(repository);

        useCase.Execute("Development").Select(entry => entry.Hostname)
            .ShouldBe(new[] { "z.test", "a.test", "m.test" });
    }

    [Fact]
    public void Matches_the_category_name_case_sensitively()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var useCase = new FilterEntriesByCategoryUseCase(repository);

        useCase.Execute("development").ShouldBeEmpty();
    }

    [Fact]
    public void Returns_an_empty_list_when_the_chosen_category_has_no_entries()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }),
            new CategoryGroup("Empty", Array.Empty<HostEntry>()));
        var useCase = new FilterEntriesByCategoryUseCase(repository);

        useCase.Execute("Empty").ShouldBeEmpty();
    }

    [Fact]
    public void Returns_an_empty_list_when_the_chosen_category_does_not_exist()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var useCase = new FilterEntriesByCategoryUseCase(repository);

        useCase.Execute("Nonexistent").ShouldBeEmpty();
    }

    [Fact]
    public void Returns_an_empty_list_when_there_are_no_categories()
    {
        var useCase = new FilterEntriesByCategoryUseCase(new FakeHostsFileRepository());

        useCase.Execute("Development").ShouldBeEmpty();
    }

    private sealed class FakeHostsFileRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();
        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();
        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;
    }
}
