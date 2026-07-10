using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Serializes a <see cref="HostEntry"/> back into a single <c>hosts</c>-file line — the
/// round-trip counterpart to <see cref="HostsFileParser"/>. An active entry becomes an
/// <c>&lt;ip&gt; &lt;hostname&gt;</c> line; a disabled entry is the same line commented out
/// with a leading <c>#</c>; an inline comment is appended after a trailing <c>#</c>.
/// </summary>
public sealed class HostsFileSerializer
{
    /// <summary>
    /// Renders <paramref name="entry"/> as the single line it occupies in a <c>hosts</c> file.
    /// </summary>
    public string SerializeEntry(HostEntry entry)
    {
        var line = $"{entry.IpAddress} {entry.Hostname}";

        if (entry.Comment is not null)
        {
            line += $" # {entry.Comment}";
        }

        if (entry.Status == HostEntryStatus.Disabled)
        {
            line = $"# {line}";
        }

        return line;
    }
}
