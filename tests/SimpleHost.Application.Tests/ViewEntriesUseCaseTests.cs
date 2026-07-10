using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ViewEntries;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ViewEntriesUseCaseTests
{
    [Fact]
    public void Returns_all_entries_provided_by_the_repository_in_order()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new ViewEntriesUseCase(repository);

        useCase.Execute().ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Includes_disabled_entries_so_they_can_be_shown_as_inactive()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled));
        var useCase = new ViewEntriesUseCase(repository);

        useCase.Execute().Select(entry => entry.Status)
            .ShouldBe(new[] { HostEntryStatus.Active, HostEntryStatus.Disabled });
    }

    [Fact]
    public void Returns_an_empty_list_when_there_are_no_entries()
    {
        var useCase = new ViewEntriesUseCase(new FakeHostsFileRepository());

        useCase.Execute().ShouldBeEmpty();
    }

    private sealed class FakeHostsFileRepository(params HostEntry[] entries) : IHostsFileRepository
    {
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

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
