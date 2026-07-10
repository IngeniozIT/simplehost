using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    [Fact]
    public void Reads_the_active_entries_from_the_file_on_disk()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "0.0.0.0 ads.example.com # blocked\n");

        var repository = new HostsFileRepository(_path, new HostsFileParser(), new HostsFileSerializer());

        repository.GetActiveEntries().ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
        });
    }

    [Fact]
    public void Reads_active_and_disabled_entries_from_the_file_on_disk_in_order()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "0.0.0.0 ads.example.com # blocked\n");

        var repository = new HostsFileRepository(_path, new HostsFileParser(), new HostsFileSerializer());

        repository.GetEntries().ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com", "blocked", HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Reads_the_entries_grouped_by_category_from_the_file_on_disk()
    {
        File.WriteAllText(_path,
            "127.0.0.1 localhost\n" +
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n");

        var repository = new HostsFileRepository(_path, new HostsFileParser(), new HostsFileSerializer());

        var groups = repository.GetGroupedEntries();

        groups.Select(group => group.Name).ShouldBe(new string?[] { null, "Development" });
        groups[0].Entries.ShouldBe(new[] { new HostEntry("127.0.0.1", "localhost") });
        groups[1].Entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
        });
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
