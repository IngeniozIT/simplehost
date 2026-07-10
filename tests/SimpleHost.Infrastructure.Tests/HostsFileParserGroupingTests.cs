using Shouldly;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public class HostsFileParserGroupingTests
{
    private readonly HostsFileParser _parser = new();

    [Fact]
    public void Empty_content_yields_a_single_empty_no_category_group()
    {
        var groups = _parser.ParseGroupedEntries(string.Empty);

        var group = groups.ShouldHaveSingleItem();
        group.Name.ShouldBeNull();
        group.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Entries_before_any_marker_belong_to_the_no_category_group()
    {
        var content = "127.0.0.1 localhost\n0.0.0.0 ads.example.com";

        var groups = _parser.ParseGroupedEntries(content);

        var group = groups.ShouldHaveSingleItem();
        group.Name.ShouldBeNull();
        group.Entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Groups_a_categorys_entries_under_its_marker_name()
    {
        var content =
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "127.0.0.1 web.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups.Select(g => g.Name).ShouldBe(new string?[] { null, "Development" });
        groups[0].Entries.ShouldBeEmpty();
        groups[1].Entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("127.0.0.1", "web.myapp.test"),
        });
    }

    [Fact]
    public void The_no_category_group_is_always_first_even_when_it_has_no_entries()
    {
        var content =
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups[0].Name.ShouldBeNull();
        groups[0].Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Keeps_uncategorized_entries_first_then_each_category()
    {
        var content =
            "127.0.0.1 localhost\n" +
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# [SimpleHost:Category] Blocklist\n" +
            "0.0.0.0 ads.example.com";

        var groups = _parser.ParseGroupedEntries(content);

        groups.Select(g => g.Name).ShouldBe(new string?[] { null, "Development", "Blocklist" });
        groups[0].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "localhost") });
        groups[1].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "api.myapp.test") });
        groups[2].Entries.ShouldBe(new[] { new HostEntry("0.0.0.0", "ads.example.com") });
    }

    [Fact]
    public void Preserves_the_order_in_which_category_markers_appear()
    {
        var content =
            "# [SimpleHost:Category] Zeta\n" +
            "127.0.0.1 z.test\n" +
            "# [SimpleHost:Category] Alpha\n" +
            "127.0.0.1 a.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups.Select(g => g.Name).ShouldBe(new string?[] { null, "Zeta", "Alpha" });
    }

    [Fact]
    public void Includes_disabled_entries_in_their_category_group_in_file_order()
    {
        var content =
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 web.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "0.0.0.0 ads.example.com # blocked";

        var groups = _parser.ParseGroupedEntries(content);

        groups[1].Entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Active),
        });
    }

    [Fact]
    public void A_marker_with_no_entries_below_it_yields_an_empty_but_present_group()
    {
        var content =
            "# [SimpleHost:Category] Empty\n" +
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups.Select(g => g.Name).ShouldBe(new string?[] { null, "Empty", "Development" });
        groups[1].Entries.ShouldBeEmpty();
        groups[2].Entries.ShouldHaveSingleItem();
    }

    [Fact]
    public void Reads_a_category_name_made_of_several_words()
    {
        var content =
            "# [SimpleHost:Category] Work Machines\n" +
            "127.0.0.1 api.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups[1].Name.ShouldBe("Work Machines");
    }

    [Fact]
    public void Keeps_a_hash_character_that_is_part_of_a_category_name()
    {
        var content =
            "# [SimpleHost:Category] C# Projects\n" +
            "127.0.0.1 api.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups[1].Name.ShouldBe("C# Projects");
    }

    [Fact]
    public void Recognises_a_marker_that_is_indented()
    {
        var content =
            "\t  # [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups[1].Name.ShouldBe("Development");
    }

    [Fact]
    public void Trims_whitespace_around_the_category_name()
    {
        var content =
            "#   [SimpleHost:Category]    Development   \n" +
            "127.0.0.1 api.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups[1].Name.ShouldBe("Development");
    }

    [Fact]
    public void Treats_a_marker_with_an_empty_name_as_a_plain_comment()
    {
        var content =
            "127.0.0.1 localhost\n" +
            "# [SimpleHost:Category]   \n" +
            "0.0.0.0 ads.example.com";

        var groups = _parser.ParseGroupedEntries(content);

        var group = groups.ShouldHaveSingleItem();
        group.Name.ShouldBeNull();
        group.Entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Two_markers_with_the_same_name_yield_two_separate_groups()
    {
        var content =
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 web.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups.Select(g => g.Name).ShouldBe(new string?[] { null, "Development", "Development" });
        groups[1].Entries.ShouldHaveSingleItem().Hostname.ShouldBe("api.myapp.test");
        groups[2].Entries.ShouldHaveSingleItem().Hostname.ShouldBe("web.myapp.test");
    }

    [Fact]
    public void Ignores_blank_lines_and_plain_comments_without_breaking_a_group()
    {
        var content =
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "\n" +
            "   \n" +
            "# just a note\n" +
            "127.0.0.1 web.myapp.test";

        var groups = _parser.ParseGroupedEntries(content);

        groups[1].Entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("127.0.0.1", "web.myapp.test"),
        });
    }

    [Fact]
    public void Handles_windows_style_carriage_return_line_endings()
    {
        var content =
            "127.0.0.1 localhost\r\n" +
            "# [SimpleHost:Category] Development\r\n" +
            "127.0.0.1 api.myapp.test\r\n";

        var groups = _parser.ParseGroupedEntries(content);

        groups.Select(g => g.Name).ShouldBe(new string?[] { null, "Development" });
        groups[0].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "localhost") });
        groups[1].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "api.myapp.test") });
    }

    [Fact]
    public void Rejects_null_content()
    {
        Should.Throw<ArgumentNullException>(() => _parser.ParseGroupedEntries(null!));
    }
}
