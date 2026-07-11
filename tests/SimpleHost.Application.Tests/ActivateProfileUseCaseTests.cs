using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ActivateProfile;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.Tests;

public class ActivateProfileUseCaseTests
{
    [Fact]
    public void Enables_every_entry_of_a_category_the_profile_lists()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
                new HostEntry("127.0.0.1", "web.test", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.1", "web.test", Status: HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Disables_every_entry_of_a_category_the_profile_does_not_list()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Blocklist", new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Active),
                new HostEntry("0.0.0.0", "tracker.example.com", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "tracker.example.com", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Enables_listed_categories_and_disables_the_rest_in_one_action()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Blocklist", new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Enables_the_no_category_group_when_the_profile_lists_it()
    {
        var store = new StubProfileStore(Profile.Of("Work", new string?[] { null }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Disables_the_no_category_group_when_the_profile_does_not_list_it()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void An_empty_profile_disables_every_category()
    {
        var store = new StubProfileStore(Profile.Of("Blackout", Array.Empty<string?>()));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            }),
            new CategoryGroup("Blocklist", new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Blackout");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Matches_the_category_name_case_sensitively()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Keeps_the_ip_hostname_and_comment_when_switching_status()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Blocklist" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Blocklist", new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Targets_each_original_entry_for_replacement()
    {
        var enabled = new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled);
        var disabled = new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Active);
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[] { enabled }),
            new CategoryGroup("Blocklist", new[] { disabled }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Targets.ShouldBe(new[] { enabled, disabled });
    }

    [Fact]
    public void An_empty_category_changes_nothing()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", Array.Empty<HostEntry>()),
            new CategoryGroup("Blocklist", Array.Empty<HostEntry>()));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Targets.ShouldBeEmpty();
        repository.Replacements.ShouldBeEmpty();
    }

    [Fact]
    public void A_listed_category_that_has_no_entries_group_is_ignored()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Nonexistent" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Work");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Looks_up_the_profile_by_the_given_name()
    {
        var store = new StubProfileStore(Profile.Of("Home", Array.Empty<string?>()));
        var repository = new RecordingHostsFileRepository();
        var useCase = new ActivateProfileUseCase(store, repository);

        useCase.Execute("Home");

        store.RequestedNames.ShouldBe(new[] { "Home" });
    }

    [Fact]
    public void Returns_the_resulting_grouped_state_with_updated_statuses()
    {
        var store = new StubProfileStore(Profile.Of("Work", new[] { "Development" }));
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Blocklist", new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        var result = useCase.Execute("Work");

        result.Select(group => group.Name).ShouldBe(new[] { "Development", "Blocklist" });
        result[0].Entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
        });
        result[1].Entries.ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Throws_and_changes_nothing_when_no_profile_is_saved_under_the_name()
    {
        var store = new StubProfileStore(storedProfile: null);
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            }));
        var useCase = new ActivateProfileUseCase(store, repository);

        Should.Throw<InvalidOperationException>(() => useCase.Execute("Missing"));

        repository.Targets.ShouldBeEmpty();
        repository.Replacements.ShouldBeEmpty();
    }

    private sealed class StubProfileStore(Profile? storedProfile) : IProfileStore
    {
        public List<string> RequestedNames { get; } = [];

        public Profile? Find(string name)
        {
            RequestedNames.Add(name);
            return storedProfile is not null && storedProfile.Name == name ? storedProfile : null;
        }

        public void Save(Profile profile) => throw new NotSupportedException();

        public IReadOnlyList<Profile> GetAll() => throw new NotSupportedException();
    }

    private sealed class RecordingHostsFileRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        public List<HostEntry> Targets { get; } = [];

        public List<HostEntry> Replacements { get; } = [];

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;

        public void UpdateEntry(HostEntry target, HostEntry replacement)
        {
            Targets.Add(target);
            Replacements.Add(replacement);
        }

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();
    }
}
