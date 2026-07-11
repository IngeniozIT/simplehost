using Shouldly;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Domain.Tests.Profiles;

public class ProfileTests
{
    [Fact]
    public void A_category_preset_does_not_restore_a_snapshot()
    {
        var profile = Profile.Of("Work", new[] { "Development" });

        profile.RestoresSnapshot.ShouldBeFalse();
        profile.Snapshot.ShouldBeNull();
        profile.SnapshotGroups.ShouldBeNull();
    }

    [Fact]
    public void A_snapshot_profile_captures_the_name_content_and_grouping()
    {
        var groups = new[]
        {
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
        };

        var profile = Profile.FromSnapshot("Default", "127.0.0.1 localhost\n", groups);

        profile.Name.ShouldBe("Default");
        profile.Snapshot.ShouldBe("127.0.0.1 localhost\n");
        profile.SnapshotGroups.ShouldBe(groups);
        profile.RestoresSnapshot.ShouldBeTrue();
        profile.EnabledCategories.ShouldBeEmpty();
    }

    [Fact]
    public void A_snapshot_profile_trims_surrounding_whitespace_from_the_name()
    {
        var profile = Profile.FromSnapshot("  Default  ", "content", Array.Empty<CategoryGroup>());

        profile.Name.ShouldBe("Default");
    }

    [Fact]
    public void A_snapshot_profile_keeps_the_captured_content_verbatim_without_trimming()
    {
        var profile = Profile.FromSnapshot("Default", "  # header\n\n127.0.0.1 localhost\n", Array.Empty<CategoryGroup>());

        profile.Snapshot.ShouldBe("  # header\n\n127.0.0.1 localhost\n");
    }

    [Fact]
    public void A_snapshot_profile_keeps_the_groups_in_the_order_they_were_given()
    {
        var groups = new[]
        {
            new CategoryGroup(null, Array.Empty<HostEntry>()),
            new CategoryGroup("Zeta", Array.Empty<HostEntry>()),
            new CategoryGroup("Alpha", Array.Empty<HostEntry>()),
        };

        var profile = Profile.FromSnapshot("Default", "content", groups);

        profile.SnapshotGroups!.Select(group => group.Name).ShouldBe(new[] { null, "Zeta", "Alpha" });
    }

    [Fact]
    public void A_snapshot_profile_materializes_the_groups_so_later_enumeration_is_stable()
    {
        var mutable = new List<CategoryGroup> { new(null, Array.Empty<HostEntry>()) };

        var profile = Profile.FromSnapshot("Default", "content", mutable);
        mutable.Add(new CategoryGroup("Added", Array.Empty<HostEntry>()));

        profile.SnapshotGroups.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_snapshot_profile_rejects_a_missing_or_blank_name(string? name)
    {
        Should.Throw<ArgumentException>(
            () => Profile.FromSnapshot(name!, "content", Array.Empty<CategoryGroup>()));
    }

    [Fact]
    public void A_snapshot_profile_rejects_null_content()
    {
        Should.Throw<ArgumentNullException>(
            () => Profile.FromSnapshot("Default", null!, Array.Empty<CategoryGroup>()));
    }

    [Fact]
    public void A_snapshot_profile_rejects_a_null_set_of_groups()
    {
        Should.Throw<ArgumentNullException>(
            () => Profile.FromSnapshot("Default", "content", null!));
    }

    [Fact]
    public void An_empty_snapshot_content_is_allowed()
    {
        var profile = Profile.FromSnapshot("Default", string.Empty, Array.Empty<CategoryGroup>());

        profile.Snapshot.ShouldBe(string.Empty);
        profile.RestoresSnapshot.ShouldBeTrue();
    }

    [Fact]
    public void Captures_the_name_and_the_enabled_categories()
    {
        var profile = Profile.Of("Work", new[] { "Development", "Blocklist" });

        profile.Name.ShouldBe("Work");
        profile.EnabledCategories.ShouldBe(new[] { "Development", "Blocklist" });
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_name()
    {
        var profile = Profile.Of("  Home  ", Array.Empty<string?>());

        profile.Name.ShouldBe("Home");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_or_blank_name(string? name)
    {
        Should.Throw<ArgumentException>(() => Profile.Of(name!, Array.Empty<string?>()));
    }

    [Fact]
    public void Rejects_a_null_set_of_enabled_categories()
    {
        Should.Throw<ArgumentNullException>(() => Profile.Of("Work", null!));
    }

    [Fact]
    public void Keeps_the_categories_in_the_order_they_were_given()
    {
        var profile = Profile.Of("Work", new[] { "Zeta", "Alpha", "Mu" });

        profile.EnabledCategories.ShouldBe(new[] { "Zeta", "Alpha", "Mu" });
    }

    [Fact]
    public void Collapses_a_repeated_category_to_a_single_entry()
    {
        var profile = Profile.Of("Work", new[] { "Development", "Development", "Blocklist" });

        profile.EnabledCategories.ShouldBe(new[] { "Development", "Blocklist" });
    }

    [Fact]
    public void Treats_categories_that_differ_only_in_case_as_distinct()
    {
        var profile = Profile.Of("Work", new[] { "Work", "work" });

        profile.EnabledCategories.ShouldBe(new[] { "Work", "work" });
    }

    [Fact]
    public void Allows_the_no_category_group_among_the_enabled_categories()
    {
        var profile = Profile.Of("Work", new[] { null, "Development" });

        profile.EnabledCategories.ShouldBe(new[] { null, "Development" });
    }

    [Fact]
    public void Collapses_the_no_category_group_when_listed_twice()
    {
        var profile = Profile.Of("Work", new string?[] { null, null });

        profile.EnabledCategories.ShouldBe(new string?[] { null });
    }

    [Fact]
    public void Allows_an_empty_set_meaning_a_profile_that_disables_everything()
    {
        var profile = Profile.Of("Blackout", Array.Empty<string?>());

        profile.EnabledCategories.ShouldBeEmpty();
    }
}
