using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ViewHostnameConflictsUseCaseTests
{
    [Fact]
    public void Reports_a_hostname_that_active_entries_map_to_two_different_ips()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"));
        var useCase = new ViewHostnameConflictsUseCase(repository);

        var report = useCase.Execute();

        report.HasConflicts.ShouldBeTrue();
        report.Conflicts.Count.ShouldBe(1);
        report.Conflicts[0].Hostname.ShouldBe("api.test");
        report.Conflicts[0].IpAddresses.ShouldBe(new[] { "127.0.0.1", "10.0.0.9" });
    }

    [Fact]
    public void Reports_no_conflict_when_the_active_entries_are_consistent()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.2", "web.test"));
        var useCase = new ViewHostnameConflictsUseCase(repository);

        var report = useCase.Execute();

        report.HasConflicts.ShouldBeFalse();
        report.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void Draws_only_from_the_active_resolving_entries()
    {
        // The fake throws for GetEntries, so a passing test proves the use case
        // reads the active (resolving) entries rather than every parsed entry.
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"));
        var useCase = new ViewHostnameConflictsUseCase(repository);

        Should.NotThrow(() => useCase.Execute());
    }

    private sealed class FakeHostsFileRepository(params HostEntry[] activeEntries) : IHostsFileRepository
    {
        public IReadOnlyList<HostEntry> GetActiveEntries() => activeEntries;
        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();
        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();
        public void CreateCategory(string name) => throw new NotSupportedException();
        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();
        public void DeleteCategory(string name) => throw new NotSupportedException();
        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();
        public void MoveEntryToCategory(HostEntry entry, string? targetCategory) => throw new NotSupportedException();
        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex) => throw new NotSupportedException();
        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();
        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();
    }
}
