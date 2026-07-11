using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.RenameProfile;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.Tests;

public class RenameProfileUseCaseTests
{
    [Fact]
    public void Renames_a_stored_category_preset_under_the_new_name()
    {
        var store = new RecordingProfileStore(Profile.Of("Work", new[] { "Development" }));
        var useCase = new RenameProfileUseCase(store);

        var renamed = useCase.Execute("Work", "Office");

        renamed.ShouldNotBeNull();
        renamed.Name.ShouldBe("Office");
        renamed.EnabledCategories.ShouldBe(new[] { "Development" });
    }

    [Fact]
    public void Removes_the_old_name_and_saves_the_renamed_profile()
    {
        var store = new RecordingProfileStore(Profile.Of("Work", new[] { "Development" }));
        var useCase = new RenameProfileUseCase(store);

        useCase.Execute("Work", "Office");

        store.Removed.ShouldBe(new[] { "Work" });
        store.Saved.ShouldHaveSingleItem().Name.ShouldBe("Office");
    }

    [Fact]
    public void Keeps_the_snapshot_shape_when_renaming_a_snapshot()
    {
        var snapshot = Profile.FromSnapshot(
            "Default",
            "127.0.0.1 localhost\n",
            new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }) });
        var store = new RecordingProfileStore(snapshot);
        var useCase = new RenameProfileUseCase(store);

        var renamed = useCase.Execute("Default", "Original");

        renamed!.RestoresSnapshot.ShouldBeTrue();
        renamed.Snapshot.ShouldBe("127.0.0.1 localhost\n");
        renamed.SnapshotGroups!.Select(group => group.Name).ShouldBe(new string?[] { null });
    }

    [Fact]
    public void Trims_the_new_name_before_saving()
    {
        var store = new RecordingProfileStore(Profile.Of("Work", new[] { "Development" }));

        new RenameProfileUseCase(store).Execute("Work", "  Office  ")!.Name.ShouldBe("Office");
    }

    [Fact]
    public void Returns_null_and_changes_nothing_when_no_profile_is_stored_under_the_old_name()
    {
        var store = new RecordingProfileStore(storedProfile: null);
        var useCase = new RenameProfileUseCase(store);

        var renamed = useCase.Execute("Missing", "Office");

        renamed.ShouldBeNull();
        store.Removed.ShouldBeEmpty();
        store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_blank_new_name_without_removing_or_saving()
    {
        var store = new RecordingProfileStore(Profile.Of("Work", new[] { "Development" }));
        var useCase = new RenameProfileUseCase(store);

        Should.Throw<ArgumentException>(() => useCase.Execute("Work", "   "));

        store.Removed.ShouldBeEmpty();
        store.Saved.ShouldBeEmpty();
    }

    private sealed class RecordingProfileStore(Profile? storedProfile) : IProfileStore
    {
        public List<Profile> Saved { get; } = [];

        public List<string> Removed { get; } = [];

        public Profile? Find(string name)
            => storedProfile is not null && storedProfile.Name == name ? storedProfile : null;

        public void Save(Profile profile) => Saved.Add(profile);

        public void Remove(string name) => Removed.Add(name);

        public IReadOnlyList<Profile> GetAll() => throw new NotSupportedException();
    }
}
