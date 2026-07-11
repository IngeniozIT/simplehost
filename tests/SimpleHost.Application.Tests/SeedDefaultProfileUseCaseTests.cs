using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.SeedDefaultProfile;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.Tests;

public class SeedDefaultProfileUseCaseTests
{
    [Fact]
    public void Seeds_a_default_snapshot_profile_capturing_the_current_content_and_grouping()
    {
        var store = new RecordingProfileStore();
        var content = new StubContentAccessor("# original\n127.0.0.1 localhost\n");
        var repository = new StubRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }));
        var useCase = new SeedDefaultProfileUseCase(store, content, repository);

        useCase.Execute();

        var seeded = store.Saved.ShouldHaveSingleItem();
        seeded.Name.ShouldBe("Default");
        seeded.RestoresSnapshot.ShouldBeTrue();
        seeded.Snapshot.ShouldBe("# original\n127.0.0.1 localhost\n");
        seeded.SnapshotGroups!.Single().Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "localhost") });
    }

    [Fact]
    public void Saves_the_snapshot_under_the_documented_default_profile_name()
    {
        var store = new RecordingProfileStore();
        var useCase = new SeedDefaultProfileUseCase(
            store, new StubContentAccessor("content"), new StubRepository());

        useCase.Execute();

        store.RequestedNames.ShouldBe(new[] { SeedDefaultProfileUseCase.DefaultProfileName });
        store.Saved.ShouldHaveSingleItem().Name.ShouldBe(SeedDefaultProfileUseCase.DefaultProfileName);
    }

    [Fact]
    public void Does_nothing_when_a_default_profile_already_exists()
    {
        var existing = Profile.FromSnapshot("Default", "# first run\n", Array.Empty<CategoryGroup>());
        var store = new RecordingProfileStore(existing);
        var content = new StubContentAccessor("# changed since\n");
        var repository = new StubRepository();
        var useCase = new SeedDefaultProfileUseCase(store, content, repository);

        useCase.Execute();

        store.Saved.ShouldBeEmpty();
        content.ReadCount.ShouldBe(0);
    }

    private sealed class RecordingProfileStore(Profile? existing = null) : IProfileStore
    {
        public List<Profile> Saved { get; } = [];

        public List<string> RequestedNames { get; } = [];

        public void Save(Profile profile) => Saved.Add(profile);

        public Profile? Find(string name)
        {
            RequestedNames.Add(name);
            return existing is not null && existing.Name == name ? existing : null;
        }

        public IReadOnlyList<Profile> GetAll() => throw new NotSupportedException();

        public void Remove(string name) => throw new NotSupportedException();
    }

    private sealed class StubContentAccessor(string content) : IHostsFileContentAccessor
    {
        public int ReadCount { get; private set; }

        public string ReadContent()
        {
            ReadCount++;
            return content;
        }

        public void ReplaceContent(string content) => throw new NotSupportedException();
    }

    private sealed class StubRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;

        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();
    }
}
