using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ReorderEntry;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ReorderEntryUseCaseTests
{
    [Fact]
    public void Reorders_the_entry_to_the_chosen_position()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ReorderEntryUseCase(repository);

        useCase.Execute(new HostEntry("10.0.0.1", "web.work"), 2);

        repository.ReorderedEntry.ShouldBe(new HostEntry("10.0.0.1", "web.work"));
        repository.TargetIndex.ShouldBe(2);
        repository.ReorderWasRequested.ShouldBeTrue();
    }

    [Fact]
    public void Reorders_the_entry_to_the_top_of_its_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ReorderEntryUseCase(repository);

        useCase.Execute(new HostEntry("10.0.0.1", "web.work"), 0);

        repository.TargetIndex.ShouldBe(0);
    }

    [Fact]
    public void Returns_the_reordered_entry_unchanged_in_content()
    {
        var useCase = new ReorderEntryUseCase(new RecordingHostsFileRepository());
        var entry = new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);

        useCase.Execute(entry, 1).ShouldBe(entry);
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public HostEntry? ReorderedEntry { get; private set; }

        public int? TargetIndex { get; private set; }

        public bool ReorderWasRequested { get; private set; }

        public void ReorderEntry(HostEntry entry, int targetIndex)
        {
            ReorderedEntry = entry;
            TargetIndex = targetIndex;
            ReorderWasRequested = true;
        }

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
