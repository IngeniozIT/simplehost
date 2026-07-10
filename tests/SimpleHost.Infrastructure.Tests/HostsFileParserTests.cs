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
    public void Captures_a_trailing_inline_comment_on_an_active_entry()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 web.myapp.test # local frontend");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "web.myapp.test", "local frontend"));
    }

    [Fact]
    public void An_active_entry_without_a_comment_has_no_comment()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 localhost");

        entries.ShouldHaveSingleItem().Comment.ShouldBeNull();
    }

    [Fact]
    public void Trims_whitespace_around_a_captured_inline_comment()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 web.myapp.test #   local frontend   ");

        entries.ShouldHaveSingleItem().Comment.ShouldBe("local frontend");
    }

    [Fact]
    public void Treats_an_empty_inline_comment_as_no_comment()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 web.myapp.test #");

        entries.ShouldHaveSingleItem().Comment.ShouldBeNull();
    }

    [Fact]
    public void Treats_a_whitespace_only_inline_comment_as_no_comment()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 web.myapp.test #    ");

        entries.ShouldHaveSingleItem().Comment.ShouldBeNull();
    }

    [Fact]
    public void Keeps_further_hash_characters_inside_a_captured_comment()
    {
        var entries = _parser.ParseActiveEntries("127.0.0.1 web.myapp.test # see issue #42");

        entries.ShouldHaveSingleItem().Comment.ShouldBe("see issue #42");
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

    [Fact]
    public void Parsing_all_entries_marks_an_uncommented_entry_as_active()
    {
        var entries = _parser.ParseEntries("127.0.0.1 localhost");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "localhost", Status: HostEntryStatus.Active));
    }

    [Fact]
    public void Parsing_all_entries_reads_a_disabled_entry_marked_disabled()
    {
        var entries = _parser.ParseEntries("# 127.0.0.1 staging.myapp.test");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled));
    }

    [Fact]
    public void Reads_a_disabled_entry_with_no_space_after_the_hash()
    {
        var entries = _parser.ParseEntries("#127.0.0.1 staging.myapp.test");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled));
    }

    [Fact]
    public void Reads_a_disabled_entry_with_extra_whitespace_before_the_hash()
    {
        var entries = _parser.ParseEntries("\t  # 127.0.0.1 staging.myapp.test");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled));
    }

    [Fact]
    public void Captures_the_inline_comment_on_a_disabled_entry()
    {
        var entries = _parser.ParseEntries("# 127.0.0.1 staging.myapp.test # was flaky");

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "staging.myapp.test", "was flaky", HostEntryStatus.Disabled));
    }

    [Fact]
    public void Keeps_active_and_disabled_entries_together_in_file_order()
    {
        var content =
            "127.0.0.1 web.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "0.0.0.0 ads.example.com";

        var entries = _parser.ParseEntries(content);

        entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Active),
        });
    }

    [Fact]
    public void Does_not_treat_a_category_marker_as_a_disabled_entry()
    {
        var content = "# [SimpleHost:Category] Development\n127.0.0.1 api.myapp.test";

        var entries = _parser.ParseEntries(content);

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "api.myapp.test", Status: HostEntryStatus.Active));
    }

    [Fact]
    public void Does_not_treat_a_plain_comment_as_a_disabled_entry()
    {
        var content = "# just a note here\n127.0.0.1 api.myapp.test";

        var entries = _parser.ParseEntries(content);

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("127.0.0.1", "api.myapp.test", Status: HostEntryStatus.Active));
    }

    [Fact]
    public void Does_not_treat_a_commented_line_whose_first_token_is_not_an_ip_as_disabled()
    {
        _parser.ParseEntries("# notanip somehost").ShouldBeEmpty();
    }

    [Fact]
    public void Does_not_treat_a_commented_line_without_a_hostname_as_a_disabled_entry()
    {
        _parser.ParseEntries("# 127.0.0.1").ShouldBeEmpty();
    }

    [Fact]
    public void Does_not_treat_a_double_commented_line_as_a_disabled_entry()
    {
        _parser.ParseEntries("## 127.0.0.1 staging.myapp.test").ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_blank_lines_when_parsing_all_entries()
    {
        var content = "\n   \n127.0.0.1 web.myapp.test\n\t\n# 0.0.0.0 ads.example.com\n";

        var entries = _parser.ParseEntries(content);

        entries.ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test", Status: HostEntryStatus.Active),
            new HostEntry("0.0.0.0", "ads.example.com", Status: HostEntryStatus.Disabled),
        });
    }

    [Fact]
    public void Ignores_a_bare_ip_line_with_no_hostname_when_parsing_all_entries()
    {
        var content = "127.0.0.1\n0.0.0.0 web.myapp.test";

        var entries = _parser.ParseEntries(content);

        entries.ShouldHaveSingleItem()
            .ShouldBe(new HostEntry("0.0.0.0", "web.myapp.test", Status: HostEntryStatus.Active));
    }

    [Fact]
    public void Parsing_all_entries_rejects_null_content()
    {
        Should.Throw<ArgumentNullException>(() => _parser.ParseEntries(null!));
    }
}
