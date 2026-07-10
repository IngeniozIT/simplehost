using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Parses the textual content of a <c>hosts</c> file into its active entries.
/// An active entry is an uncommented line of the form <c>&lt;ip&gt; &lt;hostname&gt;</c>;
/// blank lines, full-line comments (including category markers and disabled entries)
/// and trailing inline comments are ignored.
/// </summary>
public sealed class HostsFileParser
{
    public IReadOnlyList<HostEntry> ParseActiveEntries(string content)
    {
        var entries = new List<HostEntry>();
        using var reader = new StringReader(content);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TryParseActiveLine(line, out var entry))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static bool TryParseActiveLine(string line, out HostEntry entry)
    {
        entry = null!;

        // Everything from the first '#' onward is a comment. A line whose first
        // non-whitespace character is '#' therefore yields no tokens and is skipped,
        // which covers full-line comments, category markers and disabled entries alike.
        var commentStart = line.IndexOf('#');
        var uncommented = commentStart >= 0 ? line[..commentStart] : line;

        var tokens = uncommented.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        entry = new HostEntry(tokens[0], tokens[1]);
        return true;
    }
}
