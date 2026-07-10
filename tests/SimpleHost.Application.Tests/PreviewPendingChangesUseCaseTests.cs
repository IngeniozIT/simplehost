using Shouldly;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.PreviewPendingChanges;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Preview;

namespace SimpleHost.Application.Tests;

public class PreviewPendingChangesUseCaseTests
{
    [Fact]
    public void Reports_no_changes_when_nothing_is_pending()
    {
        var repository = new StubHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }));
        var useCase = new PreviewPendingChangesUseCase(repository);

        var diff = useCase.Execute(new PendingChangeSet());

        diff.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Previews_a_pending_addition_as_an_added_line_in_context()
    {
        var repository = new StubHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }));
        var useCase = new PreviewPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"));

        var diff = useCase.Execute(changes);

        diff.Lines.ShouldBe(new[]
        {
            new HostsFileDiffLine(
                HostsFileDiffLineKind.Unchanged,
                new HostEntryPlacement(null, new HostEntry("127.0.0.1", "web.test"))),
            new HostsFileDiffLine(
                HostsFileDiffLineKind.Added,
                new HostEntryPlacement(null, new HostEntry("10.0.0.1", "api.test"))),
        });
    }

    [Fact]
    public void Previews_a_pending_deletion_as_a_removed_line()
    {
        var repository = new StubHostsFileRepository(
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "web.test"),
                new HostEntry("10.0.0.1", "api.test"),
            }));
        var useCase = new PreviewPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.DeleteEntry(new HostEntry("10.0.0.1", "api.test"));

        var diff = useCase.Execute(changes);

        diff.Lines.Single(line => line.Kind == HostsFileDiffLineKind.Removed)
            .Placement.Entry.ShouldBe(new HostEntry("10.0.0.1", "api.test"));
    }

    [Fact]
    public void Reads_the_current_entries_only_from_the_repository_and_never_writes()
    {
        var repository = new StubHostsFileRepository(new CategoryGroup(null, Array.Empty<HostEntry>()));
        var useCase = new PreviewPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("127.0.0.1", "web.test"));

        useCase.Execute(changes);

        repository.WriteWasAttempted.ShouldBeFalse();
        repository.GroupedReads.ShouldBe(1);
    }

    private sealed class StubHostsFileRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        public void DeleteCategory(string name) => throw new NotSupportedException();

        public int GroupedReads { get; private set; }

        public bool WriteWasAttempted { get; private set; }

        public void CreateCategory(string name) => throw new NotSupportedException();
        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();
        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
        {
            GroupedReads++;
            return groups;
        }

        public void AddEntry(HostEntry entry, string? category = null) => WriteWasAttempted = true;

        public void UpdateEntry(HostEntry target, HostEntry replacement) => WriteWasAttempted = true;

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory) => WriteWasAttempted = true;

        public void DeleteEntry(HostEntry entry) => WriteWasAttempted = true;

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();
    }
}
