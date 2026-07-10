using Shouldly;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryRenameCategoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Rewrites_the_marker_with_the_new_name_and_keeps_its_entries()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 api.work\n");

        NewRepository().RenameCategory("Work", "Office");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Office\n10.0.0.1 web.work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Renames_only_the_target_marker_and_leaves_other_categories_untouched()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n"
            + "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().RenameCategory("Work", "Office");

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n"
            + "# [SimpleHost:Category] Office\n10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Renames_the_first_marker_when_the_name_repeats()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Work\n10.0.0.2 api.work\n");

        NewRepository().RenameCategory("Work", "Office");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Office\n10.0.0.1 web.work\n"
            + "# [SimpleHost:Category] Work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Renames_the_marker_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path, "# [SimpleHost:Category] Work");

        NewRepository().RenameCategory("Work", "Office");

        File.ReadAllText(_path).ShouldBe("# [SimpleHost:Category] Office\n");
    }

    [Fact]
    public void The_renamed_category_keeps_its_entries_in_the_grouped_view()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
        var repository = NewRepository();

        repository.RenameCategory("Work", "Office");

        var groups = repository.GetGroupedEntries();
        groups.ShouldNotContain(group => group.Name == "Work");
        var office = groups.Single(group => group.Name == "Office");
        office.Entries.Select(entry => entry.Hostname).ShouldBe(new[] { "web.work" });
    }

    [Fact]
    public void Backs_up_the_current_file_before_writing_the_renamed_marker()
    {
        File.WriteAllText(_path, "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().RenameCategory("Work", "Office");

        File.ReadAllText($"{_path}.bak").ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Throws_when_no_marker_matches_the_current_name()
    {
        File.WriteAllText(_path, "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        Should.Throw<InvalidOperationException>(
            () => NewRepository().RenameCategory("Home", "Office"));
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
