using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.SaveProfile;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.Tests;

public class SaveProfileUseCaseTests
{
    [Fact]
    public void Saves_a_profile_built_from_the_name_and_enabled_categories()
    {
        var store = new RecordingProfileStore();
        var useCase = new SaveProfileUseCase(store);

        useCase.Execute("Work", new[] { "Development", "Blocklist" });

        var saved = store.Saved.ShouldHaveSingleItem();
        saved.Name.ShouldBe("Work");
        saved.EnabledCategories.ShouldBe(new[] { "Development", "Blocklist" });
    }

    [Fact]
    public void Returns_the_profile_it_saved()
    {
        var store = new RecordingProfileStore();
        var useCase = new SaveProfileUseCase(store);

        var result = useCase.Execute("Home", new[] { "Personal" });

        result.ShouldBeSameAs(store.Saved.ShouldHaveSingleItem());
    }

    [Fact]
    public void Trims_the_profile_name_before_saving()
    {
        var store = new RecordingProfileStore();
        var useCase = new SaveProfileUseCase(store);

        var result = useCase.Execute("  Work  ", Array.Empty<string?>());

        result.Name.ShouldBe("Work");
    }

    [Fact]
    public void Deduplicates_the_enabled_categories_before_saving()
    {
        var store = new RecordingProfileStore();
        var useCase = new SaveProfileUseCase(store);

        var result = useCase.Execute("Work", new[] { "Development", "Development" });

        result.EnabledCategories.ShouldBe(new[] { "Development" });
    }

    [Fact]
    public void Saves_a_profile_with_no_enabled_categories()
    {
        var store = new RecordingProfileStore();
        var useCase = new SaveProfileUseCase(store);

        var result = useCase.Execute("Blackout", Array.Empty<string?>());

        result.EnabledCategories.ShouldBeEmpty();
        store.Saved.ShouldHaveSingleItem();
    }

    [Fact]
    public void Does_not_save_anything_when_the_name_is_blank()
    {
        var store = new RecordingProfileStore();
        var useCase = new SaveProfileUseCase(store);

        Should.Throw<ArgumentException>(() => useCase.Execute("   ", new[] { "Development" }));

        store.Saved.ShouldBeEmpty();
    }

    private sealed class RecordingProfileStore : IProfileStore
    {
        public List<Profile> Saved { get; } = [];

        public void Save(Profile profile) => Saved.Add(profile);

        public Profile? Find(string name) => throw new NotSupportedException();

        public IReadOnlyList<Profile> GetAll() => throw new NotSupportedException();

        public void Remove(string name) => throw new NotSupportedException();
    }
}
