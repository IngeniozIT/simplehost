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
    public void Writes_the_whole_configuration_as_no_category_entries_then_each_category_block()
    {
        _exporter.ExportConfiguration(
            new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Development", new[]
                {
                    new HostEntry("127.0.0.1", "api.myapp.test"),
                    new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
                }),
                new CategoryGroup("Blocklist", new[] { new HostEntry("0.0.0.0", "ads.example.com", "blocked") }),
            },
            _path);

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n" +
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "# [SimpleHost:Category] Blocklist\n" +
            "0.0.0.0 ads.example.com # blocked\n");
    }

    [Fact]
    public void Writes_an_empty_named_category_in_the_configuration_as_just_its_marker()
    {
        _exporter.ExportConfiguration(
            new[]
            {
                new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
                new CategoryGroup("Empty", Array.Empty<HostEntry>()),
                new CategoryGroup("Work", new[] { new HostEntry("127.0.0.1", "intranet.test") }),
            },
            _path);

        File.ReadAllText(_path).ShouldBe(
            "127.0.0.1 localhost\n" +
            "# [SimpleHost:Category] Empty\n" +
            "# [SimpleHost:Category] Work\n" +
            "127.0.0.1 intranet.test\n");
    }

    [Fact]
    public void Writes_an_empty_file_for_a_configuration_with_only_an_empty_no_category_group()
    {
        _exporter.ExportConfiguration(new[] { new CategoryGroup(null, Array.Empty<HostEntry>()) }, _path);

        File.ReadAllText(_path).ShouldBe(string.Empty);
    }

    [Fact]
    public void Writes_an_empty_file_for_a_configuration_with_no_groups()
    {
        _exporter.ExportConfiguration(Array.Empty<CategoryGroup>(), _path);

        File.ReadAllText(_path).ShouldBe(string.Empty);
    }

    [Fact]
    public void Overwrites_an_existing_file_when_exporting_the_whole_configuration()
    {
        File.WriteAllText(_path, "stale content that must be gone\n");

        _exporter.ExportConfiguration(
            new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }) },
            _path);

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n");
    }

    [Fact]
    public void Produces_a_standalone_hosts_file_whose_whole_configuration_round_trips_back_through_the_reader()
    {
        var noCategory = new[] { new HostEntry("127.0.0.1", "localhost") };
        var development = new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
        };
        var blocklist = new[] { new HostEntry("0.0.0.0", "ads.example.com", "blocked") };
        _exporter.ExportConfiguration(
            new[]
            {
                new CategoryGroup(null, noCategory),
                new CategoryGroup("Development", development),
                new CategoryGroup("Blocklist", blocklist),
            },
            _path);

        var groups = new HostsFileRepository(_path, new HostsFileParser(), new HostsFileSerializer())
            .GetGroupedEntries();

        groups.Select(group => group.Name).ShouldBe(new string?[] { null, "Development", "Blocklist" });
        groups[0].Entries.ShouldBe(noCategory);
        groups[1].Entries.ShouldBe(development);
        groups[2].Entries.ShouldBe(blocklist);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
