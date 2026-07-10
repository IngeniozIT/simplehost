using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.EnableSelectedEntries;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Application.Tests;

public class EnableSelectedEntriesUseCaseTests
{
    [Fact]
    public void Enables_every_entry_in_the_selection()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new EnableSelectedEntriesUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Disabled))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test", Status: HostEntryStatus.Disabled));

        useCase.Execute(selection);

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.2", "api.myapp.test", Status: HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Targets_each_original_entry_for_replacement()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new EnableSelectedEntriesUseCase(repository);
        var first = new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Disabled);
        var second = new HostEntry("127.0.0.2", "api.myapp.test", Status: HostEntryStatus.Disabled);
        var selection = EntrySelection.Empty.Select(first).Select(second);

        useCase.Execute(selection);

        repository.Targets.ShouldBe(new[] { first, second });
    }

    [Fact]
    public void Returns_the_enabled_entries_in_selection_order()
    {
        var useCase = new EnableSelectedEntriesUseCase(new RecordingHostsFileRepository());
        var selection = EntrySelection.Empty
            .Select(new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Disabled))
            .Select(new HostEntry("127.0.0.2", "api.myapp.test", Status: HostEntryStatus.Disabled));

        useCase.Execute(selection).ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.2", "api.myapp.test", Status: HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Keeps_the_ip_hostname_and_comment_when_enabling()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new EnableSelectedEntriesUseCase(repository);
        var selection = EntrySelection.Empty
            .Select(new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled));

        useCase.Execute(selection)
            .ShouldBe(new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Active),
            });
    }

    [Fact]
    public void Leaves_an_already_active_entry_active()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new EnableSelectedEntriesUseCase(repository);
        var entry = new HostEntry("127.0.0.1", "web.myapp.test");
        var selection = EntrySelection.Empty.Select(entry);

        useCase.Execute(selection).ShouldBe(new[] { entry });
        repository.Replacements.ShouldBe(new[] { entry });
    }

    [Fact]
    public void An_empty_selection_enables_nothing()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new EnableSelectedEntriesUseCase(repository);

        useCase.Execute(EntrySelection.Empty).ShouldBeEmpty();

        repository.Targets.ShouldBeEmpty();
        repository.Replacements.ShouldBeEmpty();
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public List<HostEntry> Targets { get; } = [];

        public List<HostEntry> Replacements { get; } = [];

        public void UpdateEntry(HostEntry target, HostEntry replacement)
        {
            Targets.Add(target);
            Replacements.Add(replacement);
        }

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

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => throw new NotSupportedException();
    }
}
