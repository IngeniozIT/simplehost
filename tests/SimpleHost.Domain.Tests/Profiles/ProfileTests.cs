using Shouldly;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Domain.Tests.Profiles;

public class ProfileTests
{
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
