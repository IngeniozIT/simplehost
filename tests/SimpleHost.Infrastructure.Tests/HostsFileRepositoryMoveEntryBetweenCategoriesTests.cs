using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryMoveEntryBetweenCategoriesTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Moves_the_entry_to_the_top_of_the_target_category()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n10.0.0.1 web.work\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");
    }

    [Fact]
    public void Moves_the_entry_to_a_middle_position_of_the_target_category()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 1);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n10.0.0.1 web.work\n192.168.0.2 tv.home\n");
    }

    [Fact]
    public void Moves_the_entry_to_the_bottom_of_the_target_category_when_dropped_past_the_last_entry()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 2);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Moves_the_entry_into_the_empty_target_category()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n# [SimpleHost:Category] Home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n# [SimpleHost:Category] Home\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Moves_the_entry_into_the_no_category_region_at_the_drop_position()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n10.0.0.1 uncategorized.example\n"
                + "# [SimpleHost:Category] Work\n10.0.0.9 api.work\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.9", "api.work"), null, 1);

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n10.0.0.9 api.work\n10.0.0.1 uncategorized.example\n"
                + "# [SimpleHost:Category] Work\n");
    }

    [Fact]
    public void Moves_an_uncategorized_entry_into_a_named_category_at_the_drop_position()
    {
        File.WriteAllText(_path,
            "10.0.0.1 web.work\n# [SimpleHost:Category] Work\n10.0.0.2 api.work\n10.0.0.3 db.work\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Work", 1);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 api.work\n10.0.0.1 web.work\n10.0.0.3 db.work\n");
    }

    [Fact]
    public void Preserves_the_inline_comment_of_the_moved_entry()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n0.0.0.0 ads.example.com # blocked\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().MoveEntryToCategory(
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"), "Home", 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n0.0.0.0 ads.example.com # blocked\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Keeps_a_disabled_entry_commented_out_after_the_move()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n# 0.0.0.0 ads.example.com\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().MoveEntryToCategory(
            new HostEntry("0.0.0.0", "ads.example.com", null, HostEntryStatus.Disabled), "Home", 1);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n# 0.0.0.0 ads.example.com\n");
    }

    [Fact]
    public void Counts_disabled_entries_when_resolving_the_drop_position()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n"
                + "# 0.0.0.0 blocked.home\n192.168.0.2 tv.home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 2);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n"
                + "# 0.0.0.0 blocked.home\n10.0.0.1 web.work\n192.168.0.2 tv.home\n");
    }

    [Fact]
    public void Leaves_other_categories_and_the_emptied_source_in_place()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n"
                + "# [SimpleHost:Category] Media\n10.0.0.5 plex.media\n");

        NewRepository().MoveEntryToCategory(new HostEntry("192.168.0.1", "nas.home"), "Media", 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n"
                + "# [SimpleHost:Category] Home\n"
                + "# [SimpleHost:Category] Media\n192.168.0.1 nas.home\n10.0.0.5 plex.media\n");
    }

    [Fact]
    public void Keeps_interleaved_blank_lines_and_plain_comments_of_the_target_block_in_place()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n\n# a plain note\n192.168.0.2 tv.home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 1);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n\n# a plain note\n"
                + "10.0.0.1 web.work\n192.168.0.2 tv.home\n");
    }

    [Fact]
    public void Moves_only_the_first_matching_line_when_the_entry_appears_twice()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n10.0.0.1 web.work\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n10.0.0.1 web.work\n192.168.0.1 nas.home\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Moves_the_entry_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 0);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n10.0.0.1 web.work\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void The_moved_entry_appears_in_its_new_category_and_position_in_the_grouped_view()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n192.168.0.2 tv.home\n");
        var repository = NewRepository();

        repository.MoveEntryToCategory(new HostEntry("10.0.0.1", "api.work"), "Home", 1);

        var groups = repository.GetGroupedEntries();
        groups.Single(group => group.Name == "Work").Entries.ShouldBeEmpty();
        groups.Single(group => group.Name == "Home").Entries.ShouldBe(new[]
        {
            new HostEntry("192.168.0.1", "nas.home"),
            new HostEntry("10.0.0.1", "api.work"),
            new HostEntry("192.168.0.2", "tv.home"),
        });
    }

    [Fact]
    public void Backs_up_the_current_file_before_moving()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n";
        File.WriteAllText(_path, original);

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home", 0);

        File.ReadAllText($"{_path}.bak").ShouldBe(original);
    }

    [Fact]
    public void Throws_when_no_entry_matches_the_one_to_move()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.work\n# [SimpleHost:Category] Home\n");

        var move = () => NewRepository().MoveEntryToCategory(
            new HostEntry("10.0.0.9", "absent.work"), "Home", 0);

        move.ShouldThrow<InvalidOperationException>().Message.ShouldContain("absent.work");
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_the_target_category_has_no_marker()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n";
        File.WriteAllText(_path, original);

        var move = () => NewRepository().MoveEntryToCategory(
            new HostEntry("10.0.0.1", "web.work"), "Nonexistent", 0);

        move.ShouldThrow<InvalidOperationException>().Message.ShouldContain("Nonexistent");
        File.ReadAllText(_path).ShouldBe(original);
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_the_target_is_beyond_the_droppable_range()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n";
        File.WriteAllText(_path, original);

        var move = () => NewRepository().MoveEntryToCategory(
            new HostEntry("10.0.0.1", "web.work"), "Home", 2);

        var exception = move.ShouldThrow<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("targetIndex");
        exception.Message.ShouldContain("1");
        File.ReadAllText(_path).ShouldBe(original);
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_the_target_is_negative()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n";
        File.WriteAllText(_path, original);

        var move = () => NewRepository().MoveEntryToCategory(
            new HostEntry("10.0.0.1", "web.work"), "Home", -1);

        move.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("targetIndex");
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
