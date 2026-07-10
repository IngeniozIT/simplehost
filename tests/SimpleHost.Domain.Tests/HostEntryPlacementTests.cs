using Shouldly;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Preview;

namespace SimpleHost.Domain.Tests;

public class HostEntryPlacementTests
{
    [Fact]
    public void Pairs_each_entry_with_its_category_in_file_order()
    {
        var groups = new[]
        {
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }),
            new CategoryGroup("Work", new[]
            {
                new HostEntry("10.0.0.1", "api.test"),
                new HostEntry("10.0.0.2", "db.test"),
            }),
        };

        var placements = HostEntryPlacement.Flatten(groups);

        placements.ShouldBe(new[]
        {
            new HostEntryPlacement(null, new HostEntry("127.0.0.1", "web.test")),
            new HostEntryPlacement("Work", new HostEntry("10.0.0.1", "api.test")),
            new HostEntryPlacement("Work", new HostEntry("10.0.0.2", "db.test")),
        });
    }

    [Fact]
    public void Skips_empty_categories_because_they_hold_no_entry_lines()
    {
        var groups = new[]
        {
            new CategoryGroup(null, Array.Empty<HostEntry>()),
            new CategoryGroup("Empty", Array.Empty<HostEntry>()),
            new CategoryGroup("Home", new[] { new HostEntry("127.0.0.1", "web.test") }),
        };

        var placements = HostEntryPlacement.Flatten(groups);

        placements.ShouldHaveSingleItem()
            .ShouldBe(new HostEntryPlacement("Home", new HostEntry("127.0.0.1", "web.test")));
    }
}
