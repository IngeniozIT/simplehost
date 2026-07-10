using Shouldly;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Tests;

public class HostEntryTests
{
    [Fact]
    public void Exposes_the_ip_address_and_hostname_it_was_created_with()
    {
        var entry = new HostEntry("127.0.0.1", "localhost");

        entry.IpAddress.ShouldBe("127.0.0.1");
        entry.Hostname.ShouldBe("localhost");
    }

    [Fact]
    public void Two_entries_with_the_same_ip_and_hostname_are_equal()
    {
        var a = new HostEntry("0.0.0.0", "ads.example.com");
        var b = new HostEntry("0.0.0.0", "ads.example.com");

        a.ShouldBe(b);
    }

    [Fact]
    public void Entries_differing_in_hostname_are_not_equal()
    {
        var a = new HostEntry("127.0.0.1", "web.test");
        var b = new HostEntry("127.0.0.1", "api.test");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Entries_differing_in_ip_are_not_equal()
    {
        var a = new HostEntry("127.0.0.1", "web.test");
        var b = new HostEntry("127.0.0.2", "web.test");

        a.ShouldNotBe(b);
    }
}
