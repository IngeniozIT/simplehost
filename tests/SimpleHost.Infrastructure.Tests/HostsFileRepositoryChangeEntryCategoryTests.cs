using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryChangeEntryCategoryTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Moves_the_entry_into_the_target_category_as_its_last_line()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Moves_the_entry_into_the_no_category_region_before_the_first_marker()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), null);

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n10.0.0.1 web.work\n# [SimpleHost:Category] Work\n");
    }

    [Fact]
    public void Moves_an_uncategorized_entry_into_a_named_category()
    {
        File.WriteAllText(_path,
            "10.0.0.1 web.work\n# [SimpleHost:Category] Work\n10.0.0.2 api.work\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.2 api.work\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Preserves_the_inline_comment_of_the_moved_entry()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n0.0.0.0 ads.example.com # blocked\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().MoveEntryToCategory(
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"), "Home");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n0.0.0.0 ads.example.com # blocked\n");
    }

    [Fact]
    public void Keeps_a_disabled_entry_commented_out_after_the_move()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n# 0.0.0.0 ads.example.com\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().MoveEntryToCategory(
            new HostEntry("0.0.0.0", "ads.example.com", null, HostEntryStatus.Disabled), "Home");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n# 0.0.0.0 ads.example.com\n");
    }

    [Fact]
    public void Moves_an_entry_that_sits_after_its_target_block()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n"
                + "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Work\n");
    }

    [Fact]
    public void Moves_the_entry_into_an_empty_target_block_and_leaves_later_categories_untouched()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n"
                + "# [SimpleHost:Category] Blocked\n0.0.0.0 ads.example.com\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Blocked\n0.0.0.0 ads.example.com\n");
    }

    [Fact]
    public void Moves_the_entry_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n# [SimpleHost:Category] Home");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n# [SimpleHost:Category] Home\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void The_moved_entry_appears_under_its_new_category_in_the_grouped_view()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");
        var repository = NewRepository();

        repository.MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home");

        var home = repository.GetGroupedEntries().Single(group => group.Name == "Home");
        home.Entries.ShouldBe(new[]
        {
            new HostEntry("192.168.0.1", "nas.home"),
            new HostEntry("10.0.0.1", "web.work"),
        });
    }

    [Fact]
    public void Moves_only_the_first_matching_line_when_the_entry_appears_twice()
    {
        File.WriteAllText(_path,
            "10.0.0.1 web.work\n10.0.0.1 web.work\n# [SimpleHost:Category] Home\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Home");

        File.ReadAllText(_path).ShouldBe(
            "10.0.0.1 web.work\n# [SimpleHost:Category] Home\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_the_target_category_does_not_exist()
    {
        const string original =
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n";
        File.WriteAllText(_path, original);

        var move = () => NewRepository().MoveEntryToCategory(
            new HostEntry("10.0.0.1", "web.work"), "Nonexistent");

        move.ShouldThrow<InvalidOperationException>().Message.ShouldContain("Nonexistent");
        File.ReadAllText(_path).ShouldBe(original);
    }

    [Fact]
    public void Throws_when_no_entry_matches_the_one_to_move()
    {
        File.WriteAllText(_path, "# [SimpleHost:Category] Home\n");

        var move = () => NewRepository().MoveEntryToCategory(
            new HostEntry("10.0.0.1", "absent.work"), "Home");

        move.ShouldThrow<InvalidOperationException>().Message.ShouldContain("absent.work");
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
