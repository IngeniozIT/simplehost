using Shouldly;
using SimpleHost.Application.Tray;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class TrayMenuFactoryTests
{
    private static readonly HostEntry ActiveEntry = new("127.0.0.1", "a.test");
    private static readonly HostEntry DisabledEntry = new("127.0.0.1", "b.test", Status: HostEntryStatus.Disabled);

    [Fact]
    public void Names_the_tray_icon_after_the_app()
    {
        new TrayMenuFactory().Create().Tooltip.ShouldBe("SimpleHost");
    }

    [Fact]
    public void Offers_opening_the_app_then_exiting_as_the_quick_actions()
    {
        new TrayMenuFactory().Create().Items.ShouldBe(new[]
        {
            new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
            new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
        });
    }

    [Fact]
    public void With_nothing_to_toggle_offers_only_opening_then_exiting()
    {
        Menu(categories: [], profiles: []).Items.ShouldBe(new[]
        {
            new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
            new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
        });
    }

    [Fact]
    public void Offers_disabling_a_category_whose_entries_are_all_active()
    {
        Menu(categories: [new CategoryGroup("Work", new[] { ActiveEntry, ActiveEntry })], profiles: [])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Disable Work", TrayMenuItemKind.DisableCategory, "Work"),
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Offers_enabling_a_category_that_has_any_disabled_entry()
    {
        Menu(categories: [new CategoryGroup("Work", new[] { ActiveEntry, DisabledEntry })], profiles: [])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Enable Work", TrayMenuItemKind.EnableCategory, "Work"),
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Offers_enabling_a_category_whose_entries_are_all_disabled()
    {
        Menu(categories: [new CategoryGroup("Work", new[] { DisabledEntry })], profiles: [])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Enable Work", TrayMenuItemKind.EnableCategory, "Work"),
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Names_the_no_category_group_and_targets_it_with_null()
    {
        Menu(categories: [new CategoryGroup(null, new[] { ActiveEntry })], profiles: [])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Disable No category", TrayMenuItemKind.DisableCategory, null),
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Skips_a_category_that_has_no_entries()
    {
        Menu(categories: [new CategoryGroup("Empty", Array.Empty<HostEntry>())], profiles: [])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Keeps_the_category_toggles_in_the_given_order()
    {
        Menu(
            categories:
            [
                new CategoryGroup(null, new[] { ActiveEntry }),
                new CategoryGroup("Work", new[] { DisabledEntry }),
            ],
            profiles: [])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Disable No category", TrayMenuItemKind.DisableCategory, null),
                new TrayMenuItem("Enable Work", TrayMenuItemKind.EnableCategory, "Work"),
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Offers_switching_to_each_saved_profile_in_order()
    {
        Menu(categories: [], profiles: ["Home", "Work"])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Switch to Home", TrayMenuItemKind.ActivateProfile, "Home"),
                new TrayMenuItem("Switch to Work", TrayMenuItemKind.ActivateProfile, "Work"),
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Lists_category_toggles_then_profile_switches_then_the_standing_actions()
    {
        Menu(
            categories: [new CategoryGroup("Work", new[] { ActiveEntry })],
            profiles: ["Home"])
            .Items.ShouldBe(new[]
            {
                new TrayMenuItem("Disable Work", TrayMenuItemKind.DisableCategory, "Work"),
                new TrayMenuItem("Switch to Home", TrayMenuItemKind.ActivateProfile, "Home"),
                new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
                new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
            });
    }

    [Fact]
    public void Still_names_the_tray_icon_after_the_app_when_given_toggles()
    {
        Menu(categories: [new CategoryGroup("Work", new[] { ActiveEntry })], profiles: ["Home"])
            .Tooltip.ShouldBe("SimpleHost");
    }

    [Fact]
    public void Rejects_missing_categories()
        => Should.Throw<ArgumentNullException>(
            () => new TrayMenuFactory().Create(null!, Array.Empty<string>()))
            .ParamName.ShouldBe("categories");

    [Fact]
    public void Rejects_missing_profile_names()
        => Should.Throw<ArgumentNullException>(
            () => new TrayMenuFactory().Create(Array.Empty<CategoryGroup>(), null!))
            .ParamName.ShouldBe("profileNames");

    private static TrayMenu Menu(IReadOnlyList<CategoryGroup> categories, IReadOnlyList<string> profiles)
        => new TrayMenuFactory().Create(categories, profiles);
}
