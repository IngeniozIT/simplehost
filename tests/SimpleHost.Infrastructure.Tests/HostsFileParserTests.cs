using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public class HostsFileParserTests
{
    private readonly HostsFileParser _parser = new();

    [Fact]
    public void Reads_a_single_active_entry_with_its_ip_and_hostname()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 localhost");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "localhost"));
    }

    [Fact]
    public void Reads_every_active_entry_in_file_order()
    {
        var content = "127.0.0.1 localhost\n0.0.0.0 ads.example.com\n192.168.1.10 nas.home";

        var entries = _parser.ParseActiveEntries(content);

        entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("192.168.1.10", "nas.home"),
        });
    }

    [Fact]
    public void Ignores_blank_and_whitespace_only_lines()
    {
        var content = "127.0.0.1 localhost\n\n   \n\t\n0.0.0.0 tracker.example.net";

        var entries = _parser.ParseActiveEntries(content);

        entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("0.0.0.0", "tracker.example.net"),
        });
    }

    [Fact]
    public void Ignores_full_line_comments()
    {
        var content = "# a plain comment\n127.0.0.1 localhost";

        var entries = _parser.ParseActiveEntries(content);

        entries.ShouldHaveSingleItem().Hostname.ShouldBe("localhost");
    }

    [Fact]
    public void Ignores_comments_that_are_indented()
    {
        var content = "\t  # indented comment\n127.0.0.1 localhost";

        _parser.ParseActiveEntries(content).ShouldHaveSingleItem();
    }

    [Fact]
    public void Ignores_category_marker_comments()
    {
        var content = "# [SimpleHost:Category] Development\n127.0.0.1 api.myapp.test";

        var entries = _parser.ParseActiveEntries(content);

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "api.myapp.test"));
    }

    [Fact]
    public void Ignores_disabled_entries_that_are_commented_out()
    {
        var content = "# 127.0.0.1 staging.myapp.test\n127.0.0.1 web.myapp.test";

        var entries = _parser.ParseActiveEntries(content);

        entries.ShouldHaveSingleItem().Hostname.ShouldBe("web.myapp.test");
    }

    [Fact]
    public void Strips_a_trailing_inline_comment_from_an_active_entry()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 web.myapp.test # local frontend");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test"));
    }

    [Fact]
    public void Collapses_runs_of_spaces_and_tabs_between_ip_and_hostname()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 \t   localhost");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "localhost"));
    }

    [Fact]
    public void Trims_leading_and_trailing_whitespace_around_an_active_entry()
    {
        var entries = _parser.ParseActiveEntries("   127.0.0.1 localhost   ");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "localhost"));
    }

    [Fact]
    public void Ignores_a_line_that_has_an_ip_but_no_hostname()
    {
        _parser.ParseActiveEntries("127.0.0.1").ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_a_line_whose_only_content_after_the_ip_is_an_inline_comment()
    {
        _parser.ParseActiveEntries("127.0.0.1   # dangling comment").ShouldBeEmpty();
    }

    [Fact]
    public void Handles_windows_style_carriage_return_line_endings()
    {
        var content = "127.0.0.1 localhost\r\n0.0.0.0 ads.example.com\r\n";

        var entries = _parser.ParseActiveEntries(content);

        entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    [Fact]
    public void Returns_no_entries_for_empty_content()
    {
        _parser.ParseActiveEntries(string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_null_content()
    {
        Should.Throw<ArgumentNullException>(() => _parser.ParseActiveEntries(null!));
    }
}
