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

    [Fact]
    public void Attaches_the_supplied_comment_to_the_added_entry()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new AddEntryUseCase(repository);

        useCase.Execute("0.0.0.0", "ads.example.com", "blocked ad server");

        repository.Entries.ShouldBe(
            new[] { new HostEntry("0.0.0.0", "ads.example.com", "blocked ad server") });
    }

    [Fact]
    public void Returns_the_created_entry_carrying_its_comment()
    {
        var useCase = new AddEntryUseCase(new RecordingHostsFileRepository());

        useCase.Execute("0.0.0.0", "ads.example.com", "blocked ad server")
            .ShouldBe(new HostEntry("0.0.0.0", "ads.example.com", "blocked ad server"));
    }

    [Fact]
    public void Leaves_the_entry_without_a_comment_when_none_is_supplied()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new AddEntryUseCase(repository);

        useCase.Execute("127.0.0.1", "web.myapp.test");

        repository.Entries.Single().Comment.ShouldBeNull();
    }

    [Fact]
    public void Places_the_new_entry_under_the_chosen_category()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new AddEntryUseCase(repository);

        useCase.Execute("10.0.0.1", "api.myapp.test", category: "Work");

        repository.CategoryOf(new HostEntry("10.0.0.1", "api.myapp.test")).ShouldBe("Work");
    }

    [Fact]
    public void Leaves_the_new_entry_uncategorized_when_no_category_is_chosen()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new AddEntryUseCase(repository);

        useCase.Execute("127.0.0.1", "web.myapp.test");

        repository.CategoryOf(new HostEntry("127.0.0.1", "web.myapp.test")).ShouldBeNull();
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        private readonly List<HostEntry> _entries = new();
        private readonly List<(HostEntry Entry, string? Category)> _placements = new();

        public IReadOnlyList<HostEntry> Entries => _entries;

        public string? CategoryOf(HostEntry entry)
            => _placements.Single(placement => placement.Entry == entry).Category;

        public void AddEntry(HostEntry entry, string? category = null)
        {
            _entries.Add(entry);
            _placements.Add((entry, category));
        }

        public IReadOnlyList<HostEntry> GetActiveEntries()
            => _entries.Where(entry => entry.Status == HostEntryStatus.Active).ToList();

        public IReadOnlyList<HostEntry> GetEntries() => _entries;

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
