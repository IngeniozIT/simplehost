using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Parses the textual content of a <c>hosts</c> file into its active entries.
/// An active entry is an uncommented line of the form <c>&lt;ip&gt; &lt;hostname&gt;</c>;
/// blank lines and full-line comments (including category markers and disabled entries)
/// are ignored. A trailing inline comment on an active line is captured on the entry.
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
}
