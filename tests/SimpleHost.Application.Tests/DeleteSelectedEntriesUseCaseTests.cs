using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.DeleteSelectedEntries;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Application.Tests;

public class DeleteSelectedEntriesUseCaseTests
{
    [Fact]
    public void Deletes_every_entry_in_the_selection()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteSelectedEntriesUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test"));

        useCase.Execute(selection);

        repository.Deleted.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.2", "api.myapp.test"),
        });
    }

    [Fact]
    public void Deletes_entries_in_selection_order()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteSelectedEntriesUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.3", "c.myapp.test"))
            .Select(new HostEntry("127.0.0.1", "a.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "b.myapp.test"));

        useCase.Execute(selection);

        repository.Deleted.ShouldBe(new[]
        {
            new HostEntry("127.0.0.3", "c.myapp.test"),
            new HostEntry("127.0.0.1", "a.myapp.test"),
            new HostEntry("127.0.0.2", "b.myapp.test"),
        });
    }

    [Fact]
    public void Deletes_a_disabled_entry_with_its_content_preserved_for_matching()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteSelectedEntriesUseCase(repository);
        var entry = new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);
        var selection = EntrySelection.Empty.Select(entry);

        useCase.Execute(selection);

        repository.Deleted.ShouldBe(new[] { entry });
    }

    [Fact]
    public void Returns_the_deleted_entries_in_selection_order()
    {
        var useCase = new DeleteSelectedEntriesUseCase(new RecordingHostsFileRepository());
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test"));

        useCase.Execute(selection).ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.2", "api.myapp.test"),
        });
    }

    [Fact]
    public void An_empty_selection_deletes_nothing()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteSelectedEntriesUseCase(repository);

        useCase.Execute(EntrySelection.Empty).ShouldBeEmpty();

        repository.Deleted.ShouldBeEmpty();
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public List<HostEntry> Deleted { get; } = [];

        public void DeleteEntry(HostEntry entry) => Deleted.Add(entry);

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => throw new NotSupportedException();
    }
}
