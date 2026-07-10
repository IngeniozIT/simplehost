using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.DeleteEntry;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DeleteEntryUseCaseTests
{
    [Fact]
    public void Deletes_the_given_entry_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteEntryUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"));

        repository.Deleted.ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Deletes_a_disabled_entry_with_its_comment_preserved_for_matching()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteEntryUseCase(repository);
        var entry = new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);

        useCase.Execute(entry);

        repository.Deleted.ShouldBe(entry);
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public void DeleteCategory(string name) => throw new NotSupportedException();

        public HostEntry? Deleted { get; private set; }

        public void DeleteEntry(HostEntry entry) => Deleted = entry;

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();
        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => throw new NotSupportedException();
    }
}
