using System.Net;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Parses the textual content of a <c>hosts</c> file into its entries. An active entry is an
/// uncommented line of the form <c>&lt;ip&gt; &lt;hostname&gt;</c>; a disabled entry is such a
/// line commented out with a leading <c>#</c>. Blank lines, plain comments and category
/// markers are ignored. A trailing inline comment is captured on either kind of entry.
/// </summary>
public sealed class HostsFileParser
{
    /// <summary>
    /// Parses only the active (uncommented) entries, in file order.
    /// </summary>
    public IReadOnlyList<HostEntry> ParseActiveEntries(string content)
    {
        var active = new List<HostEntry>();
        foreach (var entry in ParseEntries(content))
        {
            if (entry.Status == HostEntryStatus.Active)
            {
                active.Add(entry);
            }
        }

        return active;
    }

    /// <summary>
    /// Parses every entry — active and disabled alike — in file order, each carrying its
    /// <see cref="HostEntry.Status"/>.
    /// </summary>
    public IReadOnlyList<HostEntry> ParseEntries(string content)
    {
        var entries = new List<HostEntry>();
        using var reader = new StringReader(content);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TryParseActiveLine(line, out var active))
            {
                entries.Add(active);
            }
            else if (TryParseDisabledLine(line, out var disabled))
            {
                entries.Add(disabled);
            }
        }

        return entries;
    }

    private static bool TryParseActiveLine(string line, out HostEntry entry)
    {
        entry = null!;

        // Everything from the first '#' onward is a trailing inline comment. A line whose
        // first non-whitespace character is '#' therefore yields no tokens and is skipped,
        // which covers full-line comments, category markers and disabled entries alike.
        var commentStart = line.IndexOf('#');

        var uncommented = line;
        string? comment = null;
        if (commentStart >= 0)
        {
            uncommented = line[..commentStart];
            var text = line[(commentStart + 1)..].Trim();
            comment = text.Length > 0 ? text : null;
        }

        var tokens = uncommented.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        entry = new HostEntry(tokens[0], tokens[1], comment);
        return true;
    }

    private static bool TryParseDisabledLine(string line, out HostEntry entry)
    {
        entry = null!;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '#')
        {
            return false;
        }

        // Strip the single leading '#' that disables the line, then parse what remains as if
        // it were an active entry.
        if (!TryParseActiveLine(trimmed[1..], out var parsed))
        {
            return false;
        }

        // Only a line whose first token is a genuine IP address is a disabled entry; this
        // excludes plain comments and '# [SimpleHost:Category] <Name>' markers, whose first
        // token is not an IP address.
        if (!IPAddress.TryParse(parsed.IpAddress, out _))
        {
            return false;
        }

        entry = parsed with { Status = HostEntryStatus.Disabled };
        return true;
    }
}
