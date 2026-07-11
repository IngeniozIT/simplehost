using Shouldly;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;
using SimpleHost.Infrastructure.Hosts;
using SimpleHost.Infrastructure.Profiles;

namespace SimpleHost.Infrastructure.Tests.Profiles;

public sealed class FileProfileStoreTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-profiles-{Guid.NewGuid():N}.json");

    private FileProfileStore NewStore() => new(_path, new HostsFileParser());

    [Fact]
    public void Starts_empty_when_no_file_exists_yet()
    {
        NewStore().GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Starts_empty_when_the_file_is_blank()
    {
        File.WriteAllText(_path, "   ");

        NewStore().GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Starts_empty_when_the_file_holds_a_json_null()
    {
        File.WriteAllText(_path, "null");

        NewStore().GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Finds_a_saved_category_preset_by_name()
    {
        var store = NewStore();
        store.Save(Profile.Of("Work", new[] { "Development", "Blocklist" }));

        var found = store.Find("Work").ShouldNotBeNull();
        found.EnabledCategories.ShouldBe(new[] { "Development", "Blocklist" });
        found.RestoresSnapshot.ShouldBeFalse();
    }

    [Fact]
    public void Returns_null_when_no_profile_is_stored_under_the_name()
    {
        NewStore().Find("Missing").ShouldBeNull();
    }

    [Fact]
    public void Lists_every_saved_profile_in_the_order_they_were_saved()
    {
        var store = NewStore();
        store.Save(Profile.Of("Work", new[] { "Development" }));
        store.Save(Profile.Of("Home", new[] { "Personal" }));

        store.GetAll().Select(profile => profile.Name).ShouldBe(new[] { "Work", "Home" });
    }

    [Fact]
    public void Replaces_a_profile_saved_under_an_existing_name()
    {
        var store = NewStore();
        store.Save(Profile.Of("Work", new[] { "Development" }));
        store.Save(Profile.Of("Work", new[] { "Blocklist" }));

        store.GetAll().ShouldHaveSingleItem().EnabledCategories.ShouldBe(new[] { "Blocklist" });
    }

    [Fact]
    public void Persists_a_category_preset_across_a_restart()
    {
        NewStore().Save(Profile.Of("Work", new string?[] { null, "Development" }));

        var reopened = NewStore().Find("Work").ShouldNotBeNull();
        reopened.EnabledCategories.ShouldBe(new string?[] { null, "Development" });
    }

    [Fact]
    public void Persists_a_snapshot_profile_and_rebuilds_its_grouping_across_a_restart()
    {
        var groups = new[]
        {
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            new CategoryGroup("Development", new[]
            {
                new HostEntry("10.0.0.1", "api.test", "note", HostEntryStatus.Disabled),
            }),
        };
        NewStore().Save(Profile.FromSnapshot(
            "Default",
            "127.0.0.1 localhost\n# [SimpleHost:Category] Development\n# 10.0.0.1 api.test # note\n",
            groups));

        var reopened = NewStore().Find("Default").ShouldNotBeNull();
        reopened.RestoresSnapshot.ShouldBeTrue();
        reopened.Snapshot.ShouldBe(
            "127.0.0.1 localhost\n# [SimpleHost:Category] Development\n# 10.0.0.1 api.test # note\n");
        reopened.SnapshotGroups!.Select(group => group.Name).ShouldBe(new[] { null, "Development" });
        reopened.SnapshotGroups![1].Entries.ShouldBe(new[]
        {
            new HostEntry("10.0.0.1", "api.test", "note", HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Keeps_both_a_snapshot_and_a_preset_side_by_side_across_a_restart()
    {
        var store = NewStore();
        store.Save(Profile.FromSnapshot("Default", "127.0.0.1 localhost\n",
            new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }) }));
        store.Save(Profile.Of("Work", new[] { "Development" }));

        var reopened = NewStore();
        reopened.GetAll().Select(profile => profile.Name).ShouldBe(new[] { "Default", "Work" });
        reopened.Find("Default")!.RestoresSnapshot.ShouldBeTrue();
        reopened.Find("Work")!.RestoresSnapshot.ShouldBeFalse();
    }

    [Fact]
    public void Creates_the_containing_directory_when_it_does_not_exist()
    {
        var nestedPath = Path.Combine(
            Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}", "nested", "profiles.json");
        try
        {
            var store = new FileProfileStore(nestedPath, new HostsFileParser());
            store.Save(Profile.Of("Work", new[] { "Development" }));

            File.Exists(nestedPath).ShouldBeTrue();
        }
        finally
        {
            var root = Path.GetDirectoryName(Path.GetDirectoryName(nestedPath))!;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Rejects_a_blank_file_path()
    {
        Should.Throw<ArgumentException>(() => new FileProfileStore("   ", new HostsFileParser()));
    }

    [Fact]
    public void Rejects_a_null_parser()
    {
        Should.Throw<ArgumentNullException>(() => new FileProfileStore(_path, null!));
    }

    [Fact]
    public void Rejects_saving_a_null_profile()
    {
        Should.Throw<ArgumentNullException>(() => NewStore().Save(null!));
    }

    [Fact]
    public void Rejects_finding_a_null_name()
    {
        Should.Throw<ArgumentNullException>(() => NewStore().Find(null!));
    }

    [Fact]
    public void Removes_a_saved_profile_by_name_leaving_the_others()
    {
        var store = NewStore();
        store.Save(Profile.Of("Work", new[] { "Development" }));
        store.Save(Profile.Of("Home", new[] { "Personal" }));

        store.Remove("Work");

        store.GetAll().Select(profile => profile.Name).ShouldBe(new[] { "Home" });
        store.Find("Work").ShouldBeNull();
    }

    [Fact]
    public void Persists_a_removal_across_a_restart()
    {
        var store = NewStore();
        store.Save(Profile.Of("Work", new[] { "Development" }));
        store.Save(Profile.Of("Home", new[] { "Personal" }));

        store.Remove("Home");

        NewStore().GetAll().Select(profile => profile.Name).ShouldBe(new[] { "Work" });
    }

    [Fact]
    public void Removing_a_name_that_matches_nothing_leaves_the_store_unchanged()
    {
        var store = NewStore();
        store.Save(Profile.Of("Work", new[] { "Development" }));

        store.Remove("Missing");

        store.GetAll().Select(profile => profile.Name).ShouldBe(new[] { "Work" });
    }

    [Fact]
    public void Removing_a_missing_profile_writes_no_file()
    {
        NewStore().Remove("Missing");

        File.Exists(_path).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_removing_a_null_name()
    {
        Should.Throw<ArgumentNullException>(() => NewStore().Remove(null!));
    }

    public void Dispose() => File.Delete(_path);
}
