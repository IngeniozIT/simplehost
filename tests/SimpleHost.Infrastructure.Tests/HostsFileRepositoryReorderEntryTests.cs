using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryReorderEntryTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Reorders_the_entry_to_the_top_of_its_category()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n10.0.0.3 cache.work\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.3", "cache.work"), 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.3 cache.work\n10.0.0.1 api.work\n10.0.0.2 db.work\n");
    }

    [Fact]
    public void Reorders_the_entry_to_the_bottom_of_its_category()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n10.0.0.3 cache.work\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.1", "api.work"), 2);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 db.work\n10.0.0.3 cache.work\n10.0.0.1 api.work\n");
    }

    [Fact]
    public void Reorders_the_entry_to_a_middle_position()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n10.0.0.3 cache.work\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.1", "api.work"), 1);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 db.work\n10.0.0.1 api.work\n10.0.0.3 cache.work\n");
    }

    [Fact]
    public void Leaves_the_order_unchanged_when_the_target_is_the_current_position()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n10.0.0.3 cache.work\n";
        File.WriteAllText(_path, original);

        NewRepository().ReorderEntry(new HostEntry("10.0.0.2", "db.work"), 1);

        File.ReadAllText(_path).ShouldBe(original);
    }

    [Fact]
    public void Reorders_an_entry_within_the_no_category_region_before_the_first_marker()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n10.0.0.1 web.work\n# [SimpleHost:Category] Work\n10.0.0.2 api.work\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.1", "web.work"), 0);

        File.ReadAllText(_path).ShouldBe(
            "10.0.0.1 web.work\n127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Preserves_the_inline_comment_of_the_reordered_entry()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n0.0.0.0 ads.example.com # blocked\n10.0.0.2 db.work\n");

        NewRepository().ReorderEntry(new HostEntry("0.0.0.0", "ads.example.com", "blocked"), 1);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 db.work\n0.0.0.0 ads.example.com # blocked\n");
    }

    [Fact]
    public void Keeps_a_disabled_entry_commented_out_after_reordering()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n# 0.0.0.0 ads.example.com\n10.0.0.3 cache.work\n");

        NewRepository().ReorderEntry(
            new HostEntry("0.0.0.0", "ads.example.com", null, HostEntryStatus.Disabled), 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n# 0.0.0.0 ads.example.com\n10.0.0.1 api.work\n10.0.0.3 cache.work\n");
    }

    [Fact]
    public void Counts_disabled_entries_when_resolving_the_target_position()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n# 0.0.0.0 ads.example.com\n10.0.0.3 cache.work\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.3", "cache.work"), 1);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.3 cache.work\n# 0.0.0.0 ads.example.com\n");
    }

    [Fact]
    public void Leaves_other_categories_untouched_when_reordering_within_one()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.2", "db.work"), 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 db.work\n10.0.0.1 api.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");
    }

    [Fact]
    public void Reorders_within_the_second_category_using_its_own_marker_block()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");

        NewRepository().ReorderEntry(new HostEntry("192.168.0.2", "tv.home"), 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.2 tv.home\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Keeps_interleaved_blank_lines_and_plain_comments_in_place()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n\n# a plain note\n10.0.0.2 db.work\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.2", "db.work"), 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 db.work\n\n# a plain note\n10.0.0.1 api.work\n");
    }

    [Fact]
    public void Reorders_only_the_first_matching_line_when_the_entry_appears_twice()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 db.work\n10.0.0.1 web.work\n");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.1", "web.work"), 2);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 db.work\n10.0.0.1 web.work\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Reorders_the_entry_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work");

        NewRepository().ReorderEntry(new HostEntry("10.0.0.2", "db.work"), 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 db.work\n10.0.0.1 api.work\n");
    }

    [Fact]
    public void The_reordered_entry_appears_in_its_new_position_in_the_grouped_view()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n");
        var repository = NewRepository();

        repository.ReorderEntry(new HostEntry("10.0.0.2", "db.work"), 0);

        var work = repository.GetGroupedEntries().Single(group => group.Name == "Work");
        work.Entries.ShouldBe(new[]
        {
            new HostEntry("10.0.0.2", "db.work"),
            new HostEntry("10.0.0.1", "api.work"),
        });
    }

    [Fact]
    public void Backs_up_the_current_file_before_reordering()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n";
        File.WriteAllText(_path, original);

        NewRepository().ReorderEntry(new HostEntry("10.0.0.2", "db.work"), 0);

        File.ReadAllText($"{_path}.bak").ShouldBe(original);
    }

    [Fact]
    public void Throws_when_no_entry_matches_the_one_to_reorder()
    {
        File.WriteAllText(_path, "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n");

        var reorder = () => NewRepository().ReorderEntry(
            new HostEntry("10.0.0.9", "absent.work"), 0);

        reorder.ShouldThrow<InvalidOperationException>().Message.ShouldContain("absent.work");
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_the_target_is_beyond_the_last_entry()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n";
        File.WriteAllText(_path, original);

        var reorder = () => NewRepository().ReorderEntry(new HostEntry("10.0.0.1", "api.work"), 2);

        var exception = reorder.ShouldThrow<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("targetIndex");
        exception.Message.ShouldContain("2 entries");
        File.ReadAllText(_path).ShouldBe(original);
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_the_target_is_negative()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n10.0.0.2 db.work\n";
        File.WriteAllText(_path, original);

        var reorder = () => NewRepository().ReorderEntry(new HostEntry("10.0.0.1", "api.work"), -1);

        reorder.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("targetIndex");
        File.ReadAllText(_path).ShouldBe(original);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        if (File.Exists($"{_path}.bak"))
        {
            File.Delete($"{_path}.bak");
        }
    }
}
