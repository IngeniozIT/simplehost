using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.AddEntry;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class AddEntryUseCaseTests
{
    [Fact]
    public void Adds_an_entry_with_the_given_ip_address_and_hostname_to_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new AddEntryUseCase(repository);

        useCase.Execute("127.0.0.1", "web.myapp.test");

        repository.Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "web.myapp.test") });
    }

    [Fact]
    public void Creates_the_new_entry_as_active()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new AddEntryUseCase(repository);

        useCase.Execute("127.0.0.1", "web.myapp.test");

        repository.Entries.Single().Status.ShouldBe(HostEntryStatus.Active);
    }

    [Fact]
    public void Returns_the_created_entry()
    {
        var useCase = new AddEntryUseCase(new RecordingHostsFileRepository());

        useCase.Execute("0.0.0.0", "ads.example.com")
            .ShouldBe(new HostEntry("0.0.0.0", "ads.example.com"));
    }

    [Fact]
    public void The_added_entry_then_appears_among_the_entries()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new AddEntryUseCase(repository);

        useCase.Execute("10.0.0.1", "api.myapp.test");

        repository.GetEntries().ShouldContain(new HostEntry("10.0.0.1", "api.myapp.test"));
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        private readonly List<HostEntry> _entries = new();

        public IReadOnlyList<HostEntry> Entries => _entries;

        public void AddEntry(HostEntry entry) => _entries.Add(entry);

        public IReadOnlyList<HostEntry> GetActiveEntries()
            => _entries.Where(entry => entry.Status == HostEntryStatus.Active).ToList();

        public IReadOnlyList<HostEntry> GetEntries() => _entries;

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
