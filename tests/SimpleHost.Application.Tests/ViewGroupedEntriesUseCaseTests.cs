using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ViewGroupedEntriesUseCaseTests
{
    [Fact]
    public void Returns_the_category_groups_provided_by_the_repository_in_order()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var useCase = new ViewGroupedEntriesUseCase(repository);

        var result = useCase.Execute();

        result.Select(group => group.Name).ShouldBe(new string?[] { null, "Development" });
        result[0].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "localhost") });
        result[1].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "api.test") });
    }

    [Fact]
    public void Surfaces_the_repository_grouping_untouched()
    {
        var groups = new[] { new CategoryGroup(null, Array.Empty<HostEntry>()) };
        var useCase = new ViewGroupedEntriesUseCase(new FakeHostsFileRepository(groups));

        useCase.Execute().ShouldBeSameAs(groups);
    }

    private sealed class FakeHostsFileRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;
    }
}
