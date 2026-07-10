using Shouldly;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryCreateCategoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Appends_a_category_marker_after_the_existing_content()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        NewRepository().CreateCategory("Work");

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n");
    }

    [Fact]
    public void Appends_the_new_category_after_existing_categories_so_it_becomes_the_last_group()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n");

        NewRepository().CreateCategory("Home");

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n# [SimpleHost:Category] Home\n");
    }

    [Fact]
    public void Creates_the_marker_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost");

        NewRepository().CreateCategory("Work");

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n# [SimpleHost:Category] Work\n");
    }

    [Fact]
    public void Creates_the_marker_as_the_only_line_in_an_empty_file()
    {
        File.WriteAllText(_path, string.Empty);

        NewRepository().CreateCategory("Work");

        File.ReadAllText(_path).ShouldBe("# [SimpleHost:Category] Work\n");
    }

    [Fact]
    public void The_new_category_appears_as_an_empty_group_in_the_grouped_view()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");
        var repository = NewRepository();

        repository.CreateCategory("Work");

        var work = repository.GetGroupedEntries().Single(group => group.Name == "Work");
        work.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Backs_up_the_current_file_before_writing_the_marker()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        NewRepository().CreateCategory("Work");

        File.ReadAllText($"{_path}.bak").ShouldBe("127.0.0.1 localhost\n");
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
