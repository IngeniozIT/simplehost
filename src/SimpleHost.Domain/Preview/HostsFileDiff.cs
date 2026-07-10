namespace SimpleHost.Domain.Preview;

/// <summary>
/// A preview of what the <c>hosts</c> file will look like once a set of pending changes is
/// applied, expressed as an ordered diff against the current on-disk file: every entry line the
/// two versions share is kept as <see cref="HostsFileDiffLineKind.Unchanged"/> context, with the
/// entries only the current file has marked <see cref="HostsFileDiffLineKind.Removed"/> and those
/// only the projected file has marked <see cref="HostsFileDiffLineKind.Added"/>.
/// </summary>
/// <param name="Lines">The diff lines, in the order they appear in the file.</param>
public sealed record HostsFileDiff(IReadOnlyList<HostsFileDiffLine> Lines)
{
    /// <summary>
    /// Whether the projected file differs from the current one — <see langword="true"/> when any
    /// line is added or removed, <see langword="false"/> when the pending changes are a no-op.
    /// </summary>
    public bool HasChanges => Lines.Any(line => line.Kind != HostsFileDiffLineKind.Unchanged);

    /// <summary>
    /// Computes the diff between the entry placements of the current on-disk file and those of the
    /// projected file. Uses a longest-common-subsequence alignment so that entries untouched by the
    /// pending changes stay <see cref="HostsFileDiffLineKind.Unchanged"/> even when entries are
    /// added or removed around them, and only the genuine differences are marked.
    /// </summary>
    /// <param name="current">The entry placements as they are on disk now.</param>
    /// <param name="projected">The entry placements the file will hold once changes are applied.</param>
    public static HostsFileDiff Between(
        IReadOnlyList<HostEntryPlacement> current,
        IReadOnlyList<HostEntryPlacement> projected)
    {
        var currentCount = current.Count;
        var projectedCount = projected.Count;

        // lengths[i, j] = length of the longest common subsequence of the suffixes current[i..]
        // and projected[j..]; built from the end so a forward walk can pick the alignment that
        // preserves the most shared entries.
        var lengths = new int[currentCount + 1, projectedCount + 1];
        for (var i = currentCount - 1; i >= 0; i--)
        {
            for (var j = projectedCount - 1; j >= 0; j--)
            {
                lengths[i, j] = current[i] == projected[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var lines = new List<HostsFileDiffLine>();
        int x = 0, y = 0;
        while (x < currentCount && y < projectedCount)
        {
            if (current[x] == projected[y])
            {
                lines.Add(new HostsFileDiffLine(HostsFileDiffLineKind.Unchanged, current[x]));
                x++;
                y++;
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                lines.Add(new HostsFileDiffLine(HostsFileDiffLineKind.Removed, current[x]));
                x++;
            }
            else
            {
                lines.Add(new HostsFileDiffLine(HostsFileDiffLineKind.Added, projected[y]));
                y++;
            }
        }

        while (x < currentCount)
        {
            lines.Add(new HostsFileDiffLine(HostsFileDiffLineKind.Removed, current[x]));
            x++;
        }

        while (y < projectedCount)
        {
            lines.Add(new HostsFileDiffLine(HostsFileDiffLineKind.Added, projected[y]));
            y++;
        }

        return new HostsFileDiff(lines);
    }
}
