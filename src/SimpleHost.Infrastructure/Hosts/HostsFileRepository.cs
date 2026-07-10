using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Reads host entries from a <c>hosts</c> file on disk — active only, or active and disabled —
/// and adds new entries by appending them to the file. Every write is preceded by an automatic
/// backup of the current file contents so the previous version can always be rolled back to.
/// </summary>
public sealed class HostsFileRepository(
    string filePath,
    HostsFileParser parser,
    HostsFileSerializer serializer) : IHostsFileRepository
{
    public void AddEntry(HostEntry entry, string? category = null)
    {
        BackupCurrentFile();
        var line = serializer.SerializeEntry(entry);
        var lines = ReadContentLines(File.ReadAllText(filePath));

        lines.Insert(FindInsertionIndex(lines, category), line);

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    public void UpdateEntry(HostEntry target, HostEntry replacement)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        lines[FindEntryIndex(lines, target)] = serializer.SerializeEntry(replacement);

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        var index = FindEntryIndex(lines, entry);
        var line = lines[index];
        lines.RemoveAt(index);
        lines.Insert(FindInsertionIndex(lines, targetCategory), line);

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    public void DeleteEntry(HostEntry entry)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        lines.RemoveAt(FindEntryIndex(lines, entry));

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    /// <summary>
    /// Copies the current <c>hosts</c> file to a sibling <c>.bak</c> file, overwriting any earlier
    /// backup, so the version about to be replaced is preserved before a write is applied. Called
    /// at the start of every mutating operation, keeping the most recent previous version available
    /// to roll back to.
    /// </summary>
    private void BackupCurrentFile()
        => File.Copy(filePath, $"{filePath}.bak", overwrite: true);

    /// <summary>
    /// Returns the index of the first line that parses back to <paramref name="target"/> — matched
    /// by its parsed content rather than by raw text, so incidental whitespace differences do not
    /// prevent a match. Throws when no line matches.
    /// </summary>
    private int FindEntryIndex(List<string> lines, HostEntry target)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (parser.TryParseEntry(lines[i], out var entry) && entry == target)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"No entry found matching '{target}'.");
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
