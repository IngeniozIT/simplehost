using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.UpdateEntryComment;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class UpdateEntryCommentUseCaseTests
{
    [Fact]
    public void Sets_a_comment_on_an_entry_that_had_none()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryCommentUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "staging box");

        repository.Replacement.ShouldBe(
            new HostEntry("127.0.0.1", "web.myapp.test", "staging box"));
    }

    [Fact]
    public void Changes_an_existing_comment_to_a_new_one()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryCommentUseCase(repository);

        useCase.Execute(
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"), "ad server");

        repository.Replacement.ShouldBe(
            new HostEntry("0.0.0.0", "ads.example.com", "ad server"));
    }

    [Fact]
    public void Clears_the_comment_when_given_null()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryCommentUseCase(repository);

        useCase.Execute(new HostEntry("0.0.0.0", "ads.example.com", "blocked"), null);

        repository.Replacement.ShouldBe(new HostEntry("0.0.0.0", "ads.example.com"));
    }

    [Fact]
    public void Targets_the_original_entry_for_replacement()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryCommentUseCase(repository);

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "staging box");

        repository.Target.ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Returns_the_updated_entry_carrying_the_new_comment()
    {
        var useCase = new UpdateEntryCommentUseCase(new RecordingHostsFileRepository());

        useCase.Execute(new HostEntry("127.0.0.1", "web.myapp.test"), "staging box")
            .ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test", "staging box"));
    }

    [Fact]
    public void Keeps_the_ip_hostname_and_status_when_changing_the_comment()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new UpdateEntryCommentUseCase(repository);
        var entry = new HostEntry(
            "0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled);

        useCase.Execute(entry, "trackers")
            .ShouldBe(new HostEntry(
                "0.0.0.0", "ads.example.com", "trackers", HostEntryStatus.Disabled));
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
