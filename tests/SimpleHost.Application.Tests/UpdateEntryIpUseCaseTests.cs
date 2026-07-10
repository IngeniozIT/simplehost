using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.UpdateEntryIp;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class UpdateEntryIpUseCaseTests
{
    [Fact]
    public void Replaces_the_entry_with_one_carrying_the_new_ip_address()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryIpUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "10.0.0.1");

        repository.Replacement.ShouldBe(new HostEntry("10.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Targets_the_original_entry_for_replacement()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryIpUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "10.0.0.1");

        repository.Target.ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Returns_the_updated_entry_carrying_the_new_ip_address()
    {
        var useCase = new UpdateEntryIpUseCase(new RecordingHostsFileRepository());

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "10.0.0.1")
            .ShouldBe(new HostEntry("10.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Keeps_the_hostname_comment_and_status_when_changing_the_ip()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryIpUseCase(repository);
        var entry = new HostEntry(
            "0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);

        useCase.Execute(entry, "127.0.0.1")
            .ShouldBe(new HostEntry(
                "127.0.0.1", "ads.example.com", "blocked", HostEntryStatus.Disabled));
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

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries()
            => throw new NotSupportedException();
    }
}
