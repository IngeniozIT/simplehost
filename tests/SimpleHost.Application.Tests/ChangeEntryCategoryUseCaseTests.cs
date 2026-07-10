using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ChangeEntryCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ChangeEntryCategoryUseCaseTests
{
    [Fact]
    public void Moves_the_entry_into_the_chosen_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ChangeEntryCategoryUseCase(repository);

        useCase.Execute(new HostEntry("10.0.0.1", "web.work"), "Home");

        repository.MovedEntry.ShouldBe(new HostEntry("10.0.0.1", "web.work"));
        repository.TargetCategory.ShouldBe("Home");
    }

    [Fact]
    public void Moves_the_entry_to_no_category_when_given_null()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ChangeEntryCategoryUseCase(repository);

        useCase.Execute(new HostEntry("10.0.0.1", "web.work"), null);

        repository.MovedEntry.ShouldBe(new HostEntry("10.0.0.1", "web.work"));
        repository.TargetCategory.ShouldBeNull();
        repository.MoveWasRequested.ShouldBeTrue();
    }

    [Fact]
    public void Returns_the_entry_whose_category_changed_unchanged_in_content()
    {
        var useCase = new ChangeEntryCategoryUseCase(new RecordingHostsFileRepository());
        var entry = new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);

        useCase.Execute(entry, "Work").ShouldBe(entry);
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public HostEntry? MovedEntry { get; private set; }

        public string? TargetCategory { get; private set; }

        public bool MoveWasRequested { get; private set; }

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
        {
            MovedEntry = entry;
            TargetCategory = targetCategory;
            MoveWasRequested = true;
        }

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();
        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => throw new NotSupportedException();
    }
}
