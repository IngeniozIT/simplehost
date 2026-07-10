using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.EnableCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class EnableCategoryUseCaseTests
{
    [Fact]
    public void Enables_every_entry_in_the_chosen_category()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
                new HostEntry("127.0.0.1", "web.test", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Development");

        repository.Replacements.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.1", "web.test", Status: HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Targets_each_original_entry_for_replacement()
    {
        var first = new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled);
        var second = new HostEntry("127.0.0.1", "web.test", Status: HostEntryStatus.Disabled);
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[] { first, second }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Development");

        repository.Targets.ShouldBe(new[] { first, second });
    }

    [Fact]
    public void Returns_the_enabled_entries_in_file_order()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("0.0.0.0", "z.test", Status: HostEntryStatus.Disabled),
                new HostEntry("0.0.0.0", "a.test", Status: HostEntryStatus.Disabled),
                new HostEntry("0.0.0.0", "m.test", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Development").Select(entry => entry.Hostname)
            .ShouldBe(new[] { "z.test", "a.test", "m.test" });
    }

    [Fact]
    public void Keeps_the_ip_hostname_and_comment_when_enabling()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Blocklist", new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Disabled),
            }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Blocklist").ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Leaves_an_already_active_entry_active()
    {
        var entry = new HostEntry("127.0.0.1", "api.test");
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[] { entry }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Development").ShouldBe(new[] { entry });
        repository.Replacements.ShouldBe(new[] { entry });
    }

    [Fact]
    public void Enables_the_no_category_group_when_the_chosen_category_is_null()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup(null, new[]
            {
                new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute(null).ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Active),
        });
        repository.Targets.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Matches_the_category_name_case_sensitively()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("development").ShouldBeEmpty();
        repository.Targets.ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_category_enables_nothing()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Empty", Array.Empty<HostEntry>()));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Empty").ShouldBeEmpty();
        repository.Targets.ShouldBeEmpty();
        repository.Replacements.ShouldBeEmpty();
    }

    [Fact]
    public void A_category_that_does_not_exist_enables_nothing()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Nonexistent").ShouldBeEmpty();
        repository.Targets.ShouldBeEmpty();
    }

    [Fact]
    public void Changes_only_the_chosen_category_and_leaves_others_untouched()
    {
        var repository = new RecordingHostsFileRepository(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
            }),
            new CategoryGroup("Blocklist", new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Disabled),
            }));
        var useCase = new EnableCategoryUseCase(repository);

        useCase.Execute("Development");

        repository.Targets.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
        });
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
