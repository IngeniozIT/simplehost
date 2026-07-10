using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.UpdateEntryHostname;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class UpdateEntryHostnameUseCaseTests
{
    [Fact]
    public void Replaces_the_entry_with_one_carrying_the_new_hostname()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryHostnameUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "api.myapp.test");

        repository.Replacement.ShouldBe(new HostEntry("127.0.0.1", "api.myapp.test"));
    }

    [Fact]
    public void Targets_the_original_entry_for_replacement()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryHostnameUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "api.myapp.test");

        repository.Target.ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Returns_the_updated_entry_carrying_the_new_hostname()
    {
        var useCase = new UpdateEntryHostnameUseCase(new RecordingHostsFileRepository());

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "api.myapp.test")
            .ShouldBe(new HostEntry("127.0.0.1", "api.myapp.test"));
    }

    [Fact]
    public void Keeps_the_ip_comment_and_status_when_changing_the_hostname()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryHostnameUseCase(repository);
        var entry = new HostEntry(
            "0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);

        useCase.Execute(entry, "trackers.example.com")
            .ShouldBe(new HostEntry(
                "0.0.0.0", "trackers.example.com", "blocked", HostEntryStatus.Disabled));
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

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => throw new NotSupportedException();
    }
}
