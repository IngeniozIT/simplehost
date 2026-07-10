using Shouldly;
using SimpleHost.Domain.Duplicates;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Tests.Duplicates;

public class DuplicateEntriesTests
{
    [Fact]
    public void No_entries_yields_no_duplicates()
    {
        var report = DuplicateEntries.Of(Array.Empty<HostEntry>());

        report.HasDuplicates.ShouldBeFalse();
        report.Duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void Distinct_entries_do_not_duplicate()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.2", "web.test"),
        });

        report.HasDuplicates.ShouldBeFalse();
        report.Duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void The_same_ip_and_hostname_appearing_twice_is_a_duplicate()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "api.test"),
        });

        report.HasDuplicates.ShouldBeTrue();
        report.Duplicates.Count.ShouldBe(1);
        report.Duplicates[0].IpAddress.ShouldBe("127.0.0.1");
        report.Duplicates[0].Hostname.ShouldBe("api.test");
        report.Duplicates[0].Count.ShouldBe(2);
    }

    [Fact]
    public void The_same_ip_and_hostname_appearing_three_times_counts_all_occurrences()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "api.test"),
        });

        report.Duplicates.Count.ShouldBe(1);
        report.Duplicates[0].Count.ShouldBe(3);
    }

    [Fact]
    public void The_same_hostname_at_two_different_ips_is_not_a_duplicate()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "api.test"),
        });

        report.HasDuplicates.ShouldBeFalse();
        report.Duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void The_same_ip_with_two_different_hostnames_is_not_a_duplicate()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
        });

        report.HasDuplicates.ShouldBeFalse();
        report.Duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void Hostnames_are_matched_case_insensitively_reporting_the_first_seen_spelling()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "API.test"),
        });

        report.HasDuplicates.ShouldBeTrue();
        report.Duplicates.Count.ShouldBe(1);
        report.Duplicates[0].Hostname.ShouldBe("api.test");
        report.Duplicates[0].Count.ShouldBe(2);
    }

    [Fact]
    public void Ip_addresses_are_matched_exactly()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.01", "api.test"),
        });

        report.HasDuplicates.ShouldBeFalse();
        report.Duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void The_inline_comment_is_not_part_of_an_entry_s_identity()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test", "staging"),
            new HostEntry("127.0.0.1", "api.test", "production"),
        });

        report.HasDuplicates.ShouldBeTrue();
        report.Duplicates.Count.ShouldBe(1);
        report.Duplicates[0].IpAddress.ShouldBe("127.0.0.1");
        report.Duplicates[0].Hostname.ShouldBe("api.test");
        report.Duplicates[0].Count.ShouldBe(2);
    }

    [Fact]
    public void The_active_or_disabled_status_is_not_part_of_an_entry_s_identity()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Active),
            new HostEntry("127.0.0.1", "api.test", Status: HostEntryStatus.Disabled),
        });

        report.HasDuplicates.ShouldBeTrue();
        report.Duplicates.Count.ShouldBe(1);
        report.Duplicates[0].Count.ShouldBe(2);
    }

    [Fact]
    public void Only_the_duplicated_entry_is_reported_when_others_are_unique()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "web.test"),
            new HostEntry("10.0.0.9", "api.test"),
            new HostEntry("10.0.0.9", "api.test"),
        });

        report.Duplicates.Count.ShouldBe(1);
        report.Duplicates[0].IpAddress.ShouldBe("10.0.0.9");
        report.Duplicates[0].Hostname.ShouldBe("api.test");
    }

    [Fact]
    public void Several_duplicated_entries_are_reported_in_first_seen_order()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "web.test"),
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("10.0.0.9", "web.test"),
        });

        report.Duplicates.Count.ShouldBe(2);
        report.Duplicates.Select(duplicate => duplicate.Hostname).ShouldBe(new[] { "api.test", "web.test" });
    }

    [Fact]
    public void A_duplicate_carries_a_clear_message_naming_the_entry_and_its_occurrence_count()
    {
        var report = DuplicateEntries.Of(new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "api.test"),
        });

        report.Duplicates[0].Message.ShouldBe(
            "'127.0.0.1 api.test' is duplicated: it appears 3 times.");
    }
}
