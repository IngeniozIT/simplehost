using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryUpdateEntryTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Rewrites_the_matching_line_with_the_new_ip_address()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");

        NewRepository().UpdateEntry(
            new HostEntry("10.0.0.1", "api.myapp.test"),
            new HostEntry("192.168.1.1", "api.myapp.test"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n192.168.1.1 api.myapp.test\n");
    }

    [Fact]
    public void Rewrites_the_matching_line_with_the_new_hostname()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");

        NewRepository().UpdateEntry(
            new HostEntry("10.0.0.1", "api.myapp.test"),
            new HostEntry("10.0.0.1", "web.myapp.test"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n10.0.0.1 web.myapp.test\n");
    }

    [Fact]
    public void Preserves_the_inline_comment_on_the_updated_line()
    {
        File.WriteAllText(_path, "0.0.0.0 ads.example.com # blocked\n");

        NewRepository().UpdateEntry(
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
            new HostEntry("127.0.0.1", "ads.example.com", "blocked"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 ads.example.com # blocked\n");
    }

    [Fact]
    public void Keeps_a_disabled_entry_commented_out_after_the_update()
    {
        File.WriteAllText(_path, "# 0.0.0.0 ads.example.com\n");

        NewRepository().UpdateEntry(
            new HostEntry("0.0.0.0", "ads.example.com", null, HostEntryStatus.Disabled),
            new HostEntry("127.0.0.1", "ads.example.com", null, HostEntryStatus.Disabled));

        File.ReadAllText(_path).ShouldBe("# 127.0.0.1 ads.example.com\n");
    }

    [Fact]
    public void Leaves_the_updated_entry_in_its_category_block()
    {
        File.WriteAllText(
            _path,
            "# [SimpleHost:Category] Work\n10.0.0.1 api.myapp.test\n"
                + "# [SimpleHost:Category] Home\n192.168.0.2 nas.home\n");

        NewRepository().UpdateEntry(
            new HostEntry("10.0.0.1", "api.myapp.test"),
            new HostEntry("10.9.9.9", "api.myapp.test"));

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n10.9.9.9 api.myapp.test\n"
                + "# [SimpleHost:Category] Home\n192.168.0.2 nas.home\n");
    }

    [Fact]
    public void Updates_only_the_first_line_when_the_same_entry_appears_twice()
    {
        File.WriteAllText(_path, "10.0.0.1 api.myapp.test\n10.0.0.1 api.myapp.test\n");

        NewRepository().UpdateEntry(
            new HostEntry("10.0.0.1", "api.myapp.test"),
            new HostEntry("172.16.0.1", "api.myapp.test"));

        File.ReadAllText(_path).ShouldBe("172.16.0.1 api.myapp.test\n10.0.0.1 api.myapp.test\n");
    }

    [Fact]
    public void The_new_ip_address_appears_among_the_entries_read_back()
    {
        File.WriteAllText(_path, "10.0.0.1 api.myapp.test\n");
        var repository = NewRepository();

        repository.UpdateEntry(
            new HostEntry("10.0.0.1", "api.myapp.test"),
            new HostEntry("192.168.1.1", "api.myapp.test"));

        repository.GetEntries().ShouldBe(
            new[] { new HostEntry("192.168.1.1", "api.myapp.test") });
    }

    [Fact]
    public void Throws_when_no_entry_matches_the_target()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        var update = () => NewRepository().UpdateEntry(
            new HostEntry("10.0.0.1", "absent.myapp.test"),
            new HostEntry("192.168.1.1", "absent.myapp.test"));

        update.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("absent.myapp.test");
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
