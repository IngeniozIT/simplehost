using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Reads host entries from a <c>hosts</c> file on disk — active only, or active and disabled —
/// and adds new entries by appending them to the file.
/// </summary>
public sealed class HostsFileRepository(
    string filePath,
    HostsFileParser parser,
    HostsFileSerializer serializer) : IHostsFileRepository
{
    public void AddEntry(HostEntry entry, string? category = null)
    {
        var line = serializer.SerializeEntry(entry);
        var lines = ReadContentLines(File.ReadAllText(filePath));

        lines.Insert(FindInsertionIndex(lines, category), line);

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    /// <summary>
    /// Splits the file's text into its content lines, dropping the empty trailing element that
    /// a final newline produces so the list holds exactly the lines the file contains.
    /// </summary>
    private static List<string> ReadContentLines(string content)
    {
        if (content.Length == 0)
        {
            return new List<string>();
        }

        var lines = content.Split('\n').ToList();
        if (content.EndsWith('\n'))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>
    /// Finds the index at which the new entry's line belongs so it becomes the last entry of
    /// its target block: just before the next category marker (or at the end of the file). A
    /// <c>null</c> category targets the "No category" region before the first marker; a named
    /// category targets its marker block and requires that marker to exist.
    /// </summary>
    private static int FindInsertionIndex(List<string> lines, string? category)
    {
        var scanFrom = category is null ? 0 : IndexAfterCategoryMarker(lines, category);

        for (var i = scanFrom; i < lines.Count; i++)
        {
            if (HostsFileParser.TryParseCategoryMarker(lines[i], out _))
            {
                return i;
            }
        }

        return lines.Count;
    }

    /// <summary>
    /// Returns the index just past the <c># [SimpleHost:Category] &lt;Name&gt;</c> marker whose
    /// name matches <paramref name="category"/> — the first such marker when a name repeats.
    /// Throws when the file has no marker for that category.
    /// </summary>
    private static int IndexAfterCategoryMarker(List<string> lines, string category)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (HostsFileParser.TryParseCategoryMarker(lines[i], out var name)
                && name == category)
            {
                return i + 1;
            }
        }

        throw new InvalidOperationException($"No category marker found for '{category}'.");
    }

    public IReadOnlyList<HostEntry> GetActiveEntries()
        => parser.ParseActiveEntries(File.ReadAllText(filePath));

    public IReadOnlyList<HostEntry> GetEntries()
        => parser.ParseEntries(File.ReadAllText(filePath));

    public IReadOnlyList<CategoryGroup> GetGroupedEntries()
        => parser.ParseGroupedEntries(File.ReadAllText(filePath));
}
