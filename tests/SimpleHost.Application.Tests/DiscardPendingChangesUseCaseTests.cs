using Shouldly;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.DiscardPendingChanges;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DiscardPendingChangesUseCaseTests
{
    [Fact]
    public void Clears_every_accumulated_change()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DiscardPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"), "Work");
        changes.DeleteEntry(new HostEntry("127.0.0.1", "web.test"));
        changes.MoveEntry(new HostEntry("10.0.0.2", "db.test"), "Work");

        useCase.Execute(changes);

        changes.HasChanges.ShouldBeFalse();
        changes.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void Writes_nothing_to_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DiscardPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"), "Work");
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("192.168.0.1", "web.test"));
        changes.DeleteEntry(new HostEntry("10.0.0.2", "db.test"));

        useCase.Execute(changes);

        repository.WriteOperations.ShouldBeEmpty();
    }

    [Fact]
    public void Returns_the_current_on_disk_grouping_ignoring_the_discarded_changes()
    {
        var onDisk = new[]
        {
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            new CategoryGroup("Work", new[] { new HostEntry("10.0.0.1", "api.test") }),
        };
        var repository = new RecordingHostsFileRepository(onDisk);
        var useCase = new DiscardPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.DeleteEntry(new HostEntry("127.0.0.1", "localhost"));
        changes.AddEntry(new HostEntry("10.0.0.9", "new.test"), "Work");

        var result = useCase.Execute(changes);

        result.ShouldBe(onDisk);
        result[0].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "localhost") });
        result[1].Entries.ShouldBe(new[] { new HostEntry("10.0.0.1", "api.test") });
    }

    [Fact]
    public void Discards_an_empty_change_set_as_a_harmless_no_op()
    {
        var onDisk = new[]
        {
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
        };
        var repository = new RecordingHostsFileRepository(onDisk);
        var useCase = new DiscardPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();

        var result = useCase.Execute(changes);

        changes.HasChanges.ShouldBeFalse();
        repository.WriteOperations.ShouldBeEmpty();
        result.ShouldBe(onDisk);
    }

    private sealed class RecordingHostsFileRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        private readonly List<string> _writeOperations = new();

        public IReadOnlyList<string> WriteOperations => _writeOperations;

        public void AddEntry(HostEntry entry, string? category = null)
            => _writeOperations.Add($"Add {entry} cat={category ?? "<none>"}");

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => _writeOperations.Add($"Update {target} -> {replacement}");

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => _writeOperations.Add($"Move {entry} to {targetCategory ?? "<none>"}");

        public void DeleteEntry(HostEntry entry)
            => _writeOperations.Add($"Delete {entry}");

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();
        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;
    }
}
