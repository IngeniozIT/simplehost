using System.Net;
using SimpleHost.Domain.Categories;
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

    /// <summary>
    /// Parses every entry grouped by the category it belongs to. The first group is always the
    /// "No category" group (<see cref="CategoryGroup.Name"/> is <c>null</c>) holding the entries
    /// that appear before any marker; each subsequent group corresponds to a
    /// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker, in the order the markers appear, and
    /// carries the entries below it (active and disabled alike) in file order. A marker with no
    /// entries below it yields an empty but present group.
    /// </summary>
    public IReadOnlyList<CategoryGroup> ParseGroupedEntries(string content)
    {
        // The "No category" group is always present and always first; it collects entries that
        // appear before any category marker.
        var uncategorized = new List<HostEntry>();
        var categories = new List<(string Name, List<HostEntry> Entries)>();
        var current = uncategorized;

        using var reader = new StringReader(content);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TryParseCategoryMarker(line, out var name))
            {
                var entries = new List<HostEntry>();
                categories.Add((name, entries));
                current = entries;
            }
            else if (TryParseActiveLine(line, out var active))
            {
                current.Add(active);
            }
            else if (TryParseDisabledLine(line, out var disabled))
            {
                current.Add(disabled);
            }
        }

        var groups = new List<CategoryGroup>(categories.Count + 1)
        {
            new CategoryGroup(null, uncategorized),
        };
        foreach (var (name, entries) in categories)
        {
            groups.Add(new CategoryGroup(name, entries));
        }

        return groups;
    }

    /// <summary>
    /// Parses a single line as a host entry — active or disabled — returning
    /// <see langword="true"/> and the entry when the line is one, and <see langword="false"/> for
    /// blank lines, plain comments, and <c># [SimpleHost:Category] &lt;Name&gt;</c> markers. Lets
    /// callers match a specific line back to the <see cref="HostEntry"/> it represents (for
    /// example, to locate the line to rewrite when editing an entry).
    /// </summary>
    public bool TryParseEntry(string line, out HostEntry entry)
    {
        if (TryParseActiveLine(line, out entry))
        {
            return true;
        }

        return TryParseDisabledLine(line, out entry);
    }

    private const string CategoryMarkerPrefix = "[SimpleHost:Category]";

    /// <summary>
    /// Recognizes a <c># [SimpleHost:Category] &lt;Name&gt;</c> marker line and extracts its
    /// name. The single source of truth for the marker format, shared with the writer so that
    /// placing an entry under a category and parsing categories back agree. Returns
    /// <see langword="false"/> for blank lines, plain comments, entries, and nameless markers.
    /// </summary>
    public static bool TryParseCategoryMarker(string line, out string name)
    {
        name = null!;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '#')
        {
            return false;
        }

        // Skip the leading '#' (and any spacing after it), then require the category-marker tag.
        var afterHash = trimmed[1..].TrimStart();
        if (!afterHash.StartsWith(CategoryMarkerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // Whatever follows the tag, trimmed, is the category name. A marker with no name is not
        // a valid marker and is treated as a plain comment.
        var candidate = afterHash[CategoryMarkerPrefix.Length..].Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        name = candidate;
        return true;
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
