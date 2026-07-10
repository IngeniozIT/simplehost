using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileImporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-import-{Guid.NewGuid():N}.hosts");
    private readonly HostsFileImporter _importer = new(new HostsFileParser());

    [Fact]
    public void Reads_the_active_and_disabled_entries_in_file_order()
    {
        File.WriteAllText(
            _path,
            "127.0.0.1 api.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "0.0.0.0 ads.example.com\n");

        _importer.ImportEntries(_path).ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Captures_trailing_inline_comments()
    {
        File.WriteAllText(_path, "0.0.0.0 ads.example.com # blocked\n");

        _importer.ImportEntries(_path).ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
        });
    }

    [Fact]
    public void Flattens_entries_regardless_of_category_markers_blank_lines_and_plain_comments()
    {
        File.WriteAllText(
            _path,
            "# a plain comment\n" +
            "127.0.0.1 localhost\n" +
            "\n" +
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# [SimpleHost:Category] Blocklist\n" +
            "0.0.0.0 ads.example.com # blocked\n");

        _importer.ImportEntries(_path).ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
        });
    }

    [Fact]
    public void Returns_no_entries_for_a_file_with_only_comments_and_blank_lines()
    {
        File.WriteAllText(_path, "# just a comment\n\n# [SimpleHost:Category] Empty\n");

        _importer.ImportEntries(_path).ShouldBeEmpty();
    }

    [Fact]
    public void Returns_no_entries_for_an_empty_file()
    {
        File.WriteAllText(_path, string.Empty);

        _importer.ImportEntries(_path).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_source_path_that_is_empty_or_whitespace(string sourcePath)
        => Should.Throw<ArgumentException>(() => _importer.ImportEntries(sourcePath));

    [Fact]
    public void Rejects_a_null_source_path()
        => Should.Throw<ArgumentException>(() => _importer.ImportEntries(null!));

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
