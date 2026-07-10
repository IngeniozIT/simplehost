using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ViewActiveEntries;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ViewActiveEntriesUseCaseTests
{
    [Fact]
    public void Returns_the_active_entries_provided_by_the_repository_in_order()
    {
        var repository = new FakeHostsFileRepository(
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("0.0.0.0", "ads.example.com"));
        var useCase = new ViewActiveEntriesUseCase(repository);

        useCase.Execute().ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Returns_an_empty_list_when_there_are_no_active_entries()
    {
        var useCase = new ViewActiveEntriesUseCase(new FakeHostsFileRepository());

        useCase.Execute().ShouldBeEmpty();
    }

    private sealed class FakeHostsFileRepository(params HostEntry[] entries) : IHostsFileRepository
    {
        public IReadOnlyList<HostEntry> GetActiveEntries() => entries;
    }
}
