using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileParserEntryTests
{
    private readonly HostsFileParser _parser = new();

    [Fact]
    public void Parses_an_active_line_into_an_active_entry()
    {
        _parser.TryParseEntry("10.0.0.1 api.myapp.test # note", out var entry).ShouldBeTrue();

        entry.ShouldBe(new HostEntry("10.0.0.1", "api.myapp.test", "note"));
    }

    [Fact]
    public void Parses_a_disabled_line_into_a_disabled_entry()
    {
        _parser.TryParseEntry("# 10.0.0.1 api.myapp.test", out var entry).ShouldBeTrue();

        entry.ShouldBe(
            new HostEntry("10.0.0.1", "api.myapp.test", null, HostEntryStatus.Disabled));
    }

    [Fact]
    public void Rejects_a_category_marker_line()
    {
        _parser.TryParseEntry("# [SimpleHost:Category] Work", out _).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_plain_comment_line()
    {
        _parser.TryParseEntry("# just a note", out _).ShouldBeFalse();
    }
}
