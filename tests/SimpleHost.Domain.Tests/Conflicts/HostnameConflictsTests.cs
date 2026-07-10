using Shouldly;
using SimpleHost.Domain.Conflicts;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Tests.Conflicts;

public class HostnameConflictsTests
{
    [Fact]
    public void No_entries_yields_no_conflicts()
    {
        var report = HostnameConflicts.Of(Array.Empty<HostEntry>());

        report.HasConflicts.ShouldBeFalse();
        report.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void Distinct_hostnames_each_with_one_ip_do_not_conflict()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.2", "web.test"),
        });

        report.HasConflicts.ShouldBeFalse();
        report.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void The_same_hostname_at_the_same_ip_repeated_is_not_a_conflict()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "api.test"),
        });

        report.HasConflicts.ShouldBeFalse();
        report.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public void One_hostname_mapped_to_two_different_ips_is_a_conflict()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"),
        });

        report.HasConflicts.ShouldBeTrue();
        report.Conflicts.Count.ShouldBe(1);
        report.Conflicts[0].Hostname.ShouldBe("api.test");
        report.Conflicts[0].IpAddresses.ShouldBe(new[] { "127.0.0.1", "10.0.0.9" });
    }

    [Fact]
    public void A_hostname_mapped_to_three_different_ips_lists_all_of_them_in_first_seen_order()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"),
            new HostEntry("192.168.1.5", "api.test"),
        });

        report.Conflicts.Count.ShouldBe(1);
        report.Conflicts[0].IpAddresses.ShouldBe(new[] { "127.0.0.1", "10.0.0.9", "192.168.1.5" });
    }

    [Fact]
    public void The_same_conflicting_ip_appearing_twice_is_listed_only_once()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"),
            new HostEntry("127.0.0.1", "api.test"),
        });

        report.Conflicts.Count.ShouldBe(1);
        report.Conflicts[0].IpAddresses.ShouldBe(new[] { "127.0.0.1", "10.0.0.9" });
    }

    [Fact]
    public void Hostnames_are_matched_case_insensitively()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "API.test"),
        });

        report.HasConflicts.ShouldBeTrue();
        report.Conflicts.Count.ShouldBe(1);
        report.Conflicts[0].Hostname.ShouldBe("api.test");
        report.Conflicts[0].IpAddresses.ShouldBe(new[] { "127.0.0.1", "10.0.0.9" });
    }

    [Fact]
    public void Only_the_conflicting_hostname_is_reported_when_others_are_consistent()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"),
        });

        report.Conflicts.Count.ShouldBe(1);
        report.Conflicts[0].Hostname.ShouldBe("api.test");
    }

    [Fact]
    public void Several_conflicting_hostnames_are_reported_in_first_seen_order()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("10.0.0.9", "api.test"),
            new HostEntry("10.0.0.9", "web.test"),
        });

        report.Conflicts.Count.ShouldBe(2);
        report.Conflicts.Select(conflict => conflict.Hostname).ShouldBe(new[] { "api.test", "web.test" });
    }

    [Fact]
    public void A_conflict_carries_a_clear_message_naming_the_hostname_and_the_conflicting_ips()
    {
        var report = HostnameConflicts.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"),
        });

        report.Conflicts[0].Message.ShouldBe(
            "'api.test' is mapped to conflicting IP addresses: 127.0.0.1, 10.0.0.9.");
    }
}
