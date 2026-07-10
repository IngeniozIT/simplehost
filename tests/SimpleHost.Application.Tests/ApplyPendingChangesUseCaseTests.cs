using Shouldly;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ApplyPendingChanges;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ApplyPendingChangesUseCaseTests
{
    [Fact]
    public void Writes_a_pending_addition_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"), "Work");

        useCase.Execute(changes);

        repository.Operations.ShouldHaveSingleItem()
            .ShouldBe("Add HostEntry { IpAddress = 10.0.0.1, Hostname = api.test, "
                + "Comment = , Status = Active } cat=Work");
    }

    [Fact]
    public void Writes_a_pending_uncategorised_addition_with_no_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("127.0.0.1", "web.test"));

        useCase.Execute(changes);

        repository.Operations.ShouldHaveSingleItem()
            .ShouldBe("Add HostEntry { IpAddress = 127.0.0.1, Hostname = web.test, "
                + "Comment = , Status = Active } cat=<none>");
    }

    [Fact]
    public void Writes_a_pending_update_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.UpdateEntry(
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("192.168.0.1", "web.test"));

        useCase.Execute(changes);

        repository.Operations.ShouldHaveSingleItem()
            .ShouldBe("Update HostEntry { IpAddress = 127.0.0.1, Hostname = web.test, "
                + "Comment = , Status = Active } -> HostEntry { IpAddress = 192.168.0.1, "
                + "Hostname = web.test, Comment = , Status = Active }");
    }

    [Fact]
    public void Writes_a_pending_deletion_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.DeleteEntry(new HostEntry("10.0.0.1", "api.test"));

        useCase.Execute(changes);

        repository.Operations.ShouldHaveSingleItem()
            .ShouldBe("Delete HostEntry { IpAddress = 10.0.0.1, Hostname = api.test, "
                + "Comment = , Status = Active }");
    }

    [Fact]
    public void Writes_a_pending_move_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.MoveEntry(new HostEntry("127.0.0.1", "web.test"), "Work");

        useCase.Execute(changes);

        repository.Operations.ShouldHaveSingleItem()
            .ShouldBe("Move HostEntry { IpAddress = 127.0.0.1, Hostname = web.test, "
                + "Comment = , Status = Active } to Work");
    }

    [Fact]
    public void Writes_a_pending_move_into_no_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.MoveEntry(new HostEntry("127.0.0.1", "web.test"), null);

        useCase.Execute(changes);

        repository.Operations.ShouldHaveSingleItem()
            .ShouldBe("Move HostEntry { IpAddress = 127.0.0.1, Hostname = web.test, "
                + "Comment = , Status = Active } to <none>");
    }

    [Fact]
    public void Writes_every_accumulated_change_in_the_order_they_were_recorded()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"), "Work");
        changes.DeleteEntry(new HostEntry("127.0.0.1", "web.test"));
        changes.MoveEntry(new HostEntry("10.0.0.2", "db.test"), "Work");

        useCase.Execute(changes);

        repository.Operations.ShouldBe(new[]
        {
            "Add HostEntry { IpAddress = 10.0.0.1, Hostname = api.test, "
                + "Comment = , Status = Active } cat=Work",
            "Delete HostEntry { IpAddress = 127.0.0.1, Hostname = web.test, "
                + "Comment = , Status = Active }",
            "Move HostEntry { IpAddress = 10.0.0.2, Hostname = db.test, "
                + "Comment = , Status = Active } to Work",
        });
    }

    [Fact]
    public void Writes_nothing_when_no_changes_are_pending()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);

        useCase.Execute(new PendingChangeSet());

        repository.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void Clears_the_pending_changes_once_they_have_been_applied()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new ApplyPendingChangesUseCase(repository);
        var changes = new PendingChangeSet();
        changes.AddEntry(new HostEntry("10.0.0.1", "api.test"));

        useCase.Execute(changes);

        changes.HasChanges.ShouldBeFalse();
        changes.Changes.ShouldBeEmpty();
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        private readonly List<string> _operations = new();

        public IReadOnlyList<string> Operations => _operations;

        public void AddEntry(HostEntry entry, string? category = null)
            => _operations.Add($"Add {entry} cat={category ?? "<none>"}");

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => _operations.Add($"Update {target} -> {replacement}");

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => _operations.Add($"Move {entry} to {targetCategory ?? "<none>"}");

        public void DeleteEntry(HostEntry entry)
            => _operations.Add($"Delete {entry}");

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
