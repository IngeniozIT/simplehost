using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.MoveEntryBetweenCategories;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class MoveEntryBetweenCategoriesUseCaseTests
{
    [Fact]
    public void Moves_the_entry_into_the_chosen_category_at_the_drop_position()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveEntryBetweenCategoriesUseCase(repository);

        useCase.Execute(new HostEntry("10.0.0.1", "web.work"), "Home", 2);

        repository.MovedEntry.ShouldBe(new HostEntry("10.0.0.1", "web.work"));
        repository.TargetCategory.ShouldBe("Home");
        repository.TargetIndex.ShouldBe(2);
        repository.MoveWasRequested.ShouldBeTrue();
    }

    [Fact]
    public void Moves_the_entry_to_the_top_of_the_target_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveEntryBetweenCategoriesUseCase(repository);

        useCase.Execute(new HostEntry("10.0.0.1", "web.work"), "Home", 0);

        repository.TargetIndex.ShouldBe(0);
    }

    [Fact]
    public void Moves_the_entry_to_no_category_when_given_null()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveEntryBetweenCategoriesUseCase(repository);

        useCase.Execute(new HostEntry("10.0.0.1", "web.work"), null, 1);

        repository.TargetCategory.ShouldBeNull();
        repository.MoveWasRequested.ShouldBeTrue();
    }

    [Fact]
    public void Returns_the_moved_entry_unchanged_in_content()
    {
        var useCase = new MoveEntryBetweenCategoriesUseCase(new RecordingHostsFileRepository());
        var entry = new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);

        useCase.Execute(entry, "Work", 1).ShouldBe(entry);
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public HostEntry? MovedEntry { get; private set; }

        public string? TargetCategory { get; private set; }

        public int? TargetIndex { get; private set; }

        public bool MoveWasRequested { get; private set; }

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
        {
            MovedEntry = entry;
            TargetCategory = targetCategory;
            TargetIndex = targetIndex;
            MoveWasRequested = true;
        }

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

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
