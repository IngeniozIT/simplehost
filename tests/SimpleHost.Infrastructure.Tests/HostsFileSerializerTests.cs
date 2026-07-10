using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileSerializerTests
{
    private readonly HostsFileSerializer _serializer = new();

    [Fact]
    public void Serializes_an_active_entry_as_an_ip_and_hostname_line()
    {
        _serializer.SerializeEntry(new HostEntry("127.0.0.1", "web.myapp.test"))
            .ShouldBe("127.0.0.1 web.myapp.test");
    }

    [Fact]
    public void Serializes_an_inline_comment_after_a_hash_on_the_entry_line()
    {
        _serializer.SerializeEntry(new HostEntry("0.0.0.0", "ads.example.com", "blocked"))
            .ShouldBe("0.0.0.0 ads.example.com # blocked");
    }

    [Fact]
    public void Serializes_a_disabled_entry_as_a_commented_out_line()
    {
        _serializer.SerializeEntry(
                new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled))
            .ShouldBe("# 127.0.0.1 staging.myapp.test");
    }

    [Fact]
    public void Serializes_a_disabled_entry_with_a_comment()
    {
        _serializer.SerializeEntry(
                new HostEntry("127.0.0.1", "staging.myapp.test", "on hold", HostEntryStatus.Disabled))
            .ShouldBe("# 127.0.0.1 staging.myapp.test # on hold");
    }

    [Fact]
    public void Round_trips_every_status_and_comment_combination_through_the_parser()
    {
        var parser = new HostsFileParser();
        var entries = new[]
        {
            new HostEntry("127.0.0.1", "web.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
            new HostEntry("127.0.0.1", "staging.myapp.test", Status: HostEntryStatus.Disabled),
            new HostEntry("10.0.0.1", "api.myapp.test", "on hold", HostEntryStatus.Disabled),
        };

        foreach (var entry in entries)
        {
            parser.ParseEntries(_serializer.SerializeEntry(entry)).ShouldBe(new[] { entry });
        }
    }
}
