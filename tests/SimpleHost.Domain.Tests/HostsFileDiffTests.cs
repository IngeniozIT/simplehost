using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Preview;

namespace SimpleHost.Domain.Tests;

public class HostsFileDiffTests
{
    private static HostEntryPlacement Placement(string ip, string host, string? category = null)
        => new(category, new HostEntry(ip, host));

    [Fact]
    public void Reports_no_changes_when_current_and_projected_are_identical()
    {
        var placements = new[] { Placement("127.0.0.1", "web.test"), Placement("10.0.0.1", "api.test") };

        var diff = HostsFileDiff.Between(placements, placements);

        diff.HasChanges.ShouldBeFalse();
        diff.Lines.ShouldAllBe(line => line.Kind == HostsFileDiffLineKind.Unchanged);
    }

    [Fact]
    public void Keeps_every_shared_entry_as_unchanged_context()
    {
        var current = new[] { Placement("127.0.0.1", "web.test"), Placement("10.0.0.1", "api.test") };

        var diff = HostsFileDiff.Between(current, current);

        diff.Lines
            .Select(line => line.Placement)
            .ShouldBe(current);
    }

    [Fact]
    public void Marks_an_entry_only_the_projected_file_has_as_added()
    {
        var current = new[] { Placement("127.0.0.1", "web.test") };
        var projected = new[] { Placement("127.0.0.1", "web.test"), Placement("10.0.0.1", "api.test") };

        var diff = HostsFileDiff.Between(current, projected);

        diff.HasChanges.ShouldBeTrue();
        diff.Lines.ShouldBe(new[]
        {
            new HostsFileDiffLine(HostsFileDiffLineKind.Unchanged, Placement("127.0.0.1", "web.test")),
            new HostsFileDiffLine(HostsFileDiffLineKind.Added, Placement("10.0.0.1", "api.test")),
        });
    }

    [Fact]
    public void Marks_an_entry_only_the_current_file_has_as_removed()
    {
        var current = new[] { Placement("127.0.0.1", "web.test"), Placement("10.0.0.1", "api.test") };
        var projected = new[] { Placement("127.0.0.1", "web.test") };

        var diff = HostsFileDiff.Between(current, projected);

        diff.HasChanges.ShouldBeTrue();
        diff.Lines.ShouldBe(new[]
        {
            new HostsFileDiffLine(HostsFileDiffLineKind.Unchanged, Placement("127.0.0.1", "web.test")),
            new HostsFileDiffLine(HostsFileDiffLineKind.Removed, Placement("10.0.0.1", "api.test")),
        });
    }

    [Fact]
    public void Shows_an_edited_entry_as_the_old_line_removed_and_the_new_line_added()
    {
        var current = new[] { Placement("127.0.0.1", "web.test") };
        var projected = new[] { Placement("192.168.0.1", "web.test") };

        var diff = HostsFileDiff.Between(current, projected);

        diff.Lines.ShouldBe(new[]
        {
            new HostsFileDiffLine(HostsFileDiffLineKind.Removed, Placement("127.0.0.1", "web.test")),
            new HostsFileDiffLine(HostsFileDiffLineKind.Added, Placement("192.168.0.1", "web.test")),
        });
    }

    [Fact]
    public void Treats_the_same_entry_under_a_different_category_as_a_change()
    {
        var current = new[] { Placement("127.0.0.1", "web.test", "Work") };
        var projected = new[] { Placement("127.0.0.1", "web.test", "Home") };

        var diff = HostsFileDiff.Between(current, projected);

        diff.Lines.Select(line => line.Kind).ShouldBe(new[]
        {
            HostsFileDiffLineKind.Removed,
            HostsFileDiffLineKind.Added,
        });
    }

    [Fact]
    public void Keeps_surrounding_entries_unchanged_when_one_is_inserted_between_them()
    {
        var current = new[] { Placement("127.0.0.1", "web.test"), Placement("10.0.0.1", "api.test") };
        var projected = new[]
        {
            Placement("127.0.0.1", "web.test"),
            Placement("0.0.0.0", "ads.test"),
            Placement("10.0.0.1", "api.test"),
        };

        var diff = HostsFileDiff.Between(current, projected);

        diff.Lines.ShouldBe(new[]
        {
            new HostsFileDiffLine(HostsFileDiffLineKind.Unchanged, Placement("127.0.0.1", "web.test")),
            new HostsFileDiffLine(HostsFileDiffLineKind.Added, Placement("0.0.0.0", "ads.test")),
            new HostsFileDiffLine(HostsFileDiffLineKind.Unchanged, Placement("10.0.0.1", "api.test")),
        });
    }

    [Fact]
    public void Adds_every_entry_when_the_current_file_is_empty()
    {
        var projected = new[] { Placement("127.0.0.1", "web.test") };

        var diff = HostsFileDiff.Between(Array.Empty<HostEntryPlacement>(), projected);

        diff.Lines.ShouldHaveSingleItem().Kind.ShouldBe(HostsFileDiffLineKind.Added);
    }

    [Fact]
    public void Reports_no_changes_between_two_empty_files()
    {
        var diff = HostsFileDiff.Between(
            Array.Empty<HostEntryPlacement>(),
            Array.Empty<HostEntryPlacement>());

        diff.Lines.ShouldBeEmpty();
        diff.HasChanges.ShouldBeFalse();
    }
}
