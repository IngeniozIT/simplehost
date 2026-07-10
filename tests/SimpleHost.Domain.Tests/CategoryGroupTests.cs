using Shouldly;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Tests;

public class CategoryGroupTests
{
    [Fact]
    public void Exposes_the_category_name_it_was_created_with()
    {
        var group = new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") });

        group.Name.ShouldBe("Development");
    }

    [Fact]
    public void Exposes_the_entries_it_was_created_with_in_order()
    {
        var entries = new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
        };
        var group = new CategoryGroup("Development", entries);

        group.Entries.ShouldBe(entries);
    }

    [Fact]
    public void A_null_name_represents_the_uncategorized_no_category_group()
    {
        var group = new CategoryGroup(null, Array.Empty<HostEntry>());

        group.Name.ShouldBeNull();
    }

    [Fact]
    public void A_category_can_hold_no_entries()
    {
        var group = new CategoryGroup("Empty", Array.Empty<HostEntry>());

        group.Entries.ShouldBeEmpty();
    }
}
