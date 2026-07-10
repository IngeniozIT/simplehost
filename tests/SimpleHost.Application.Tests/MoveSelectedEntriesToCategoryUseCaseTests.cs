using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.MoveSelectedEntriesToCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Application.Tests;

public class MoveSelectedEntriesToCategoryUseCaseTests
{
    [Fact]
    public void Moves_every_entry_in_the_selection_into_the_chosen_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveSelectedEntriesToCategoryUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test"));

        useCase.Execute(selection, "Work");

        repository.Moves.ShouldBe(new[]
        {
            (new HostEntry("127.0.0.1", "web.myapp.test"), (string?)"Work"),
            (new HostEntry("127.0.0.2", "api.myapp.test"), (string?)"Work"),
        });
    }

    [Fact]
    public void Moves_entries_in_selection_order()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveSelectedEntriesToCategoryUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.3", "c.myapp.test"))
            .Select(new HostEntry("127.0.0.1", "a.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "b.myapp.test"));

        useCase.Execute(selection, "Home");

        repository.Moves.Select(move => move.Entry).ShouldBe(new[]
        {
            new HostEntry("127.0.0.3", "c.myapp.test"),
            new HostEntry("127.0.0.1", "a.myapp.test"),
            new HostEntry("127.0.0.2", "b.myapp.test"),
        });
    }

    [Fact]
    public void Sends_every_selected_entry_to_the_same_target_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveSelectedEntriesToCategoryUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test"))
            .Select(new HostEntry("127.0.0.3", "db.myapp.test"));

        useCase.Execute(selection, "Home");

        repository.Moves.Select(move => move.TargetCategory).ShouldBe(new[] { "Home", "Home", "Home" });
    }

    [Fact]
    public void Moves_the_selection_into_no_category_when_given_null()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveSelectedEntriesToCategoryUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test"));

        useCase.Execute(selection, null);

        repository.Moves.Select(move => move.TargetCategory).ShouldBe(new string?[] { null, null });
    }

    [Fact]
    public void Moves_a_disabled_entry_with_its_content_preserved_for_matching()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveSelectedEntriesToCategoryUseCase(repository);
        var entry = new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);
        var selection = EntrySelection.Empty.Select(entry);

        useCase.Execute(selection, "Work");

        repository.Moves.ShouldBe(new[] { (entry, (string?)"Work") });
    }

    [Fact]
    public void Returns_the_moved_entries_in_selection_order()
    {
        var useCase = new MoveSelectedEntriesToCategoryUseCase(new RecordingHostsFileRepository());
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test"))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test"));

        useCase.Execute(selection, "Work").ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("127.0.0.2", "api.myapp.test"),
        });
    }

    [Fact]
    public void An_empty_selection_moves_nothing()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new MoveSelectedEntriesToCategoryUseCase(repository);

        useCase.Execute(EntrySelection.Empty, "Work").ShouldBeEmpty();

        repository.Moves.ShouldBeEmpty();
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public List<(HostEntry Entry, string? TargetCategory)> Moves { get; } = [];

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => Moves.Add((entry, targetCategory));

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
