using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.DisableEntry;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DisableEntryUseCaseTests
{
    [Fact]
    public void Replaces_the_entry_with_a_disabled_copy()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DisableEntryUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"));

        repository.Replacement.ShouldBe(
            new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Disabled));
    }

    [Fact]
    public void Targets_the_original_active_entry_for_replacement()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DisableEntryUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"));

        repository.Target.ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Returns_the_disabled_entry()
    {
        var useCase = new DisableEntryUseCase(new RecordingHostsFileRepository());

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"))
            .ShouldBe(new HostEntry(
                "127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Disabled));
    }

    [Fact]
    public void Keeps_the_ip_hostname_and_comment_when_disabling()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DisableEntryUseCase(repository);
        var entry = new HostEntry("0.0.0.0", "ads.example.com", "blocked");

        useCase.Execute(entry)
            .ShouldBe(new HostEntry(
                "0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled));
    }

    [Fact]
    public void Leaves_an_already_disabled_entry_disabled()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DisableEntryUseCase(repository);
        var entry = new HostEntry(
            "0.0.0.0", "ads.example.com", Status: HostEntryStatus.Disabled);

        useCase.Execute(entry).ShouldBe(entry);
        repository.Replacement.ShouldBe(entry);
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public HostEntry? Target { get; private set; }

        public HostEntry? Replacement { get; private set; }

        public void UpdateEntry(HostEntry target, HostEntry replacement)
        {
            Target = target;
            Replacement = replacement;
        }

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => throw new NotSupportedException();
    }
}
