using Shouldly;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileExporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-export-{Guid.NewGuid():N}.hosts");
    private readonly HostsFileExporter _exporter = new(new HostsFileSerializer());

    [Fact]
    public void Writes_the_category_marker_followed_by_its_entries()
    {
        _exporter.ExportCategory(
            new CategoryGroup("Development", new[]
            {
                new HostEntry("127.0.0.1", "api.myapp.test"),
                new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
                new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
            }),
            _path);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "0.0.0.0 ads.example.com # blocked\n");
    }

    [Fact]
    public void Writes_only_the_marker_for_an_empty_category()
    {
        _exporter.ExportCategory(new CategoryGroup("Empty", Array.Empty<HostEntry>()), _path);

        File.ReadAllText(_path).ShouldBe("# [SimpleHost:Category] Empty\n");
    }

    [Fact]
    public void Writes_the_entries_without_a_marker_for_the_no_category_group()
    {
        _exporter.ExportCategory(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            _path);

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n");
    }

    [Fact]
    public void Writes_an_empty_file_for_the_empty_no_category_group()
    {
        _exporter.ExportCategory(new CategoryGroup(null, Array.Empty<HostEntry>()), _path);

        File.ReadAllText(_path).ShouldBe(string.Empty);
    }

    [Fact]
    public void Overwrites_an_existing_file_at_the_destination()
    {
        File.WriteAllText(_path, "stale content that must be gone\n");

        _exporter.ExportCategory(
            new CategoryGroup("Work", new[] { new HostEntry("127.0.0.1", "intranet.test") }),
            _path);

        File.ReadAllText(_path).ShouldBe(
            "# [SimpleHost:Category] Work\n" +
            "127.0.0.1 intranet.test\n");
    }

    [Fact]
    public void Produces_a_standalone_hosts_file_that_round_trips_back_through_the_reader()
    {
        var entries = new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
        };
        _exporter.ExportCategory(new CategoryGroup("Development", entries), _path);

        var groups = new HostsFileRepository(_path, new HostsFileParser(), new HostsFileSerializer())
            .GetGroupedEntries();

        groups.Select(group => group.Name).ShouldBe(new string?[] { null, "Development" });
        groups[0].Entries.ShouldBeEmpty();
        groups[1].Entries.ShouldBe(entries);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
