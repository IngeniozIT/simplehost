using Shouldly;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryAddEntryCategoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Writes_the_new_entry_under_the_chosen_category_marker_block()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.2", "api.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Appends_the_new_entry_as_the_last_line_of_its_category_block()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 one.work\n10.0.0.2 two.work\n# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.3", "three.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 one.work\n10.0.0.2 two.work\n10.0.0.3 three.work\n# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Adds_the_new_entry_to_an_empty_category_block_right_after_its_marker()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.1", "web.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Adds_the_new_entry_under_a_category_that_is_preceded_by_another_category()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.2", "api.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Adds_the_new_entry_under_a_category_that_is_the_last_block()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.2", "api.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Leaves_later_categories_untouched_when_adding_to_a_middle_category()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n# [SimpleHost:Category] Blocked\n0.0.0.0 ads.example.com\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.2", "api.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 api.work\n# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n# [SimpleHost:Category] Blocked\n0.0.0.0 ads.example.com\n");
    }

    [Fact]
    public void Places_the_new_entry_in_the_no_category_region_before_the_first_marker()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.9", "extra.test"));

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n10.0.0.9 extra.test\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Falls_back_to_appending_at_the_end_when_no_category_is_chosen_and_no_markers_exist()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");
    }

    [Fact]
    public void The_added_entry_appears_under_its_category_in_the_grouped_view()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");
        var repository = NewRepository();

        repository.AddEntry(new HostEntry("10.0.0.2", "api.work"), "Work");

        var work = repository.GetGroupedEntries().Single(group => group.Name == "Work");
        work.Entries.ShouldBe(new[]
        {
            new HostEntry("10.0.0.1", "web.work"),
            new HostEntry("10.0.0.2", "api.work"),
        });
    }

    [Fact]
    public void Adds_under_a_category_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work");

        NewRepository().AddEntry(new HostEntry("10.0.0.2", "api.work"), "Work");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 api.work\n");
    }

    [Fact]
    public void Throws_when_the_chosen_category_does_not_exist()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");
        var repository = NewRepository();

        Should.Throw<InvalidOperationException>(
                () => repository.AddEntry(new HostEntry("10.0.0.1", "api.work"), "Nonexistent"))
            .Message.ShouldContain("Nonexistent");
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
