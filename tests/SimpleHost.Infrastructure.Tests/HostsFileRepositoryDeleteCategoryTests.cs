using Shouldly;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryDeleteCategoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Removes_the_marker_and_moves_its_entries_back_to_no_category()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n"
            + "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().DeleteCategory("Home");

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n192.168.0.1 nas.home\n"
            + "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Deletes_only_the_target_marker_and_leaves_other_categories_untouched()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().DeleteCategory("Work");

        File.ReadAllText(_path).ShouldBe(
            "10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Moves_only_the_deleted_middle_categorys_entries_and_leaves_the_following_category_in_place()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Alpha\n10.0.0.1 a.host\n"
            + "# [SimpleHost:Category] Beta\n10.0.0.2 b.host\n"
            + "# [SimpleHost:Category] Gamma\n10.0.0.3 g.host\n");

        NewRepository().DeleteCategory("Beta");

        File.ReadAllText(_path).ShouldBe(
            "10.0.0.2 b.host\n"
            + "# [SimpleHost:Category] Alpha\n10.0.0.1 a.host\n"
            + "# [SimpleHost:Category] Gamma\n10.0.0.3 g.host\n");
    }

    [Fact]
    public void Deletes_the_first_marker_when_the_name_repeats()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Work\n10.0.0.2 api.work\n");

        NewRepository().DeleteCategory("Work");

        File.ReadAllText(_path).ShouldBe(
            "10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Removes_an_empty_category_marker_and_changes_nothing_else()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n"
            + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().DeleteCategory("Work");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Deletes_the_marker_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path, "# [SimpleHost:Category] Work\n10.0.0.1 web.work");

        NewRepository().DeleteCategory("Work");

        File.ReadAllText(_path).ShouldBe("10.0.0.1 web.work\n");
    }

    [Fact]
    public void Keeps_a_disabled_entry_when_its_category_is_deleted()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n# 10.0.0.1 web.work\n");

        NewRepository().DeleteCategory("Work");

        File.ReadAllText(_path).ShouldBe("# 10.0.0.1 web.work\n");
    }

    [Fact]
    public void The_deleted_categorys_entries_appear_under_no_category_in_the_grouped_view()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
        var repository = NewRepository();

        repository.DeleteCategory("Work");

        var groups = repository.GetGroupedEntries();
        groups.ShouldNotContain(group => group.Name == "Work");
        var uncategorized = groups.Single(group => group.Name is null);
        uncategorized.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "web.work" });
    }

    [Fact]
    public void Backs_up_the_current_file_before_deleting_the_marker()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().DeleteCategory("Work");

        File.ReadAllText($"{_path}.bak").ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_no_marker_matches()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        Should.Throw<InvalidOperationException>(() => NewRepository().DeleteCategory("Home"));

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
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
