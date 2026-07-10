using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryDeleteEntryTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Removes_the_entry_line_and_keeps_the_other_entries()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n10.0.0.1 web.work\n192.168.0.1 nas.home\n");

        NewRepository().DeleteEntry(new HostEntry("10.0.0.1", "web.work"));

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Leaves_an_empty_category_marker_when_its_last_entry_is_deleted()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");

        NewRepository().DeleteEntry(new HostEntry("10.0.0.1", "web.work"));

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n"
                + "# [SimpleHost:Category] Home\n192.168.0.1 nas.home\n");
    }

    [Fact]
    public void Deletes_a_disabled_entry_matched_by_its_parsed_content()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n# 0.0.0.0 ads.example.com\n");

        NewRepository().DeleteEntry(
            new HostEntry("0.0.0.0", "ads.example.com", null, HostEntryStatus.Disabled));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n");
    }

    [Fact]
    public void Deletes_an_entry_with_an_inline_comment()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n0.0.0.0 ads.example.com # blocked\n");

        NewRepository().DeleteEntry(new HostEntry("0.0.0.0", "ads.example.com", "blocked"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n");
    }

    [Fact]
    public void Deletes_only_the_first_matching_line_when_the_entry_appears_twice()
    {
        File.WriteAllText(_path,
            "10.0.0.1 web.work\n10.0.0.1 web.work\n127.0.0.1 localhost\n");

        NewRepository().DeleteEntry(new HostEntry("10.0.0.1", "web.work"));

        File.ReadAllText(_path).ShouldBe(
            "10.0.0.1 web.work\n127.0.0.1 localhost\n");
    }

    [Fact]
    public void Deletes_the_entry_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n10.0.0.1 web.work");

        NewRepository().DeleteEntry(new HostEntry("10.0.0.1", "web.work"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n");
    }

    [Fact]
    public void The_deleted_entry_no_longer_appears_in_the_grouped_view()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Work\n10.0.0.1 web.work\n10.0.0.2 api.work\n");
        var repository = NewRepository();

        repository.DeleteEntry(new HostEntry("10.0.0.1", "web.work"));

        var work = repository.GetGroupedEntries().Single(group => group.Name == "Work");
        work.Entries.ShouldBe(new[] { new HostEntry("10.0.0.2", "api.work") });
    }

    [Fact]
    public void Throws_and_leaves_the_file_untouched_when_no_entry_matches()
    {
        const string original = "127.0.0.1 localhost\n10.0.0.1 web.work\n";
        File.WriteAllText(_path, original);

        var delete = () => NewRepository().DeleteEntry(new HostEntry("10.0.0.1", "absent.work"));

        delete.ShouldThrow<InvalidOperationException>().Message.ShouldContain("absent.work");
        File.ReadAllText(_path).ShouldBe(original);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
