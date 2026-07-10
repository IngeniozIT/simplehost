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

    public void CreateCategory(string name)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        lines.Add(serializer.SerializeCategoryMarker(name));

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    public void RenameCategory(string currentName, string newName)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        lines[FindCategoryMarkerIndex(lines, currentName)] = serializer.SerializeCategoryMarker(newName);

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    public void DeleteCategory(string name)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        var markerIndex = FindCategoryMarkerIndex(lines, name);
        var blockEnd = FindCategoryBlockEnd(lines, markerIndex + 1);
        var entries = lines.GetRange(markerIndex + 1, blockEnd - markerIndex - 1);
        lines.RemoveRange(markerIndex, blockEnd - markerIndex);
        lines.InsertRange(FindInsertionIndex(lines, null), entries);

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

    public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        var from = FindEntryIndex(lines, entry);
        var line = lines[from];
        lines.RemoveAt(from);

        var blockStart = targetCategory is null ? 0 : IndexAfterCategoryMarker(lines, targetCategory);
        var blockEnd = FindCategoryBlockEnd(lines, blockStart);

        var slots = new List<int>();
        for (var i = blockStart; i < blockEnd; i++)
        {
            if (parser.TryParseEntry(lines[i], out _))
            {
                slots.Add(i);
            }
        }

        if (targetIndex < 0 || targetIndex > slots.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                $"Target position is outside the range 0 to {slots.Count} for the target category.");
        }

        var insertAt = targetIndex == slots.Count ? blockEnd : slots[targetIndex];
        lines.Insert(insertAt, line);

        File.WriteAllText(filePath, string.Concat(lines.Select(l => $"{l}\n")));
    }

    public void ReorderEntry(HostEntry entry, int targetIndex)
    {
        BackupCurrentFile();
        var lines = ReadContentLines(File.ReadAllText(filePath));

        var from = FindEntryIndex(lines, entry);
        var blockStart = FindGoverningMarkerIndex(lines, from) + 1;
        var blockEnd = FindCategoryBlockEnd(lines, blockStart);

        var slots = new List<int>();
        for (var i = blockStart; i < blockEnd; i++)
        {
            if (parser.TryParseEntry(lines[i], out _))
            {
                slots.Add(i);
            }
        }

        if (targetIndex < 0 || targetIndex >= slots.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                $"Target position is outside the {slots.Count} entries of the category.");
        }

        var ordinal = slots.IndexOf(from);
        var ordered = slots.Select(slot => lines[slot]).ToList();
        var moved = ordered[ordinal];
        ordered.RemoveAt(ordinal);
        ordered.Insert(targetIndex, moved);

        for (var j = 0; j < slots.Count; j++)
        {
            lines[slots[j]] = ordered[j];
        }

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
    /// Returns the index of the first <c># [SimpleHost:Category] &lt;Name&gt;</c> marker at or after
    /// <paramref name="scanFrom"/> — the line that ends the category block starting at that point — or
    /// the line count when no further marker exists, so the block runs to the end of the file.
    /// </summary>
    private static int FindCategoryBlockEnd(List<string> lines, int scanFrom)
    {
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
    /// Returns the index of the <c># [SimpleHost:Category] &lt;Name&gt;</c> marker that governs the
    /// line at <paramref name="entryIndex"/> — the last marker before it — or <c>-1</c> when no
    /// marker precedes it, meaning the line belongs to the "No category" region. The marker index
    /// plus one is the first line of the governed block.
    /// </summary>
    private static int FindGoverningMarkerIndex(List<string> lines, int entryIndex)
    {
        var marker = -1;
        for (var i = 0; i < entryIndex; i++)
        {
            if (HostsFileParser.TryParseCategoryMarker(lines[i], out _))
            {
                marker = i;
            }
        }

        return marker;
    }

    /// <summary>
    /// Returns the index just past the <c># [SimpleHost:Category] &lt;Name&gt;</c> marker whose
    /// name matches <paramref name="category"/> — the first such marker when a name repeats.
    /// Throws when the file has no marker for that category.
    /// </summary>
    private static int IndexAfterCategoryMarker(List<string> lines, string category)
        => FindCategoryMarkerIndex(lines, category) + 1;

    /// <summary>
    /// Returns the index of the <c># [SimpleHost:Category] &lt;Name&gt;</c> marker whose name matches
    /// <paramref name="category"/> — the first such marker when a name repeats. Throws when the file
    /// has no marker for that category.
    /// </summary>
    private static int FindCategoryMarkerIndex(List<string> lines, string category)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (HostsFileParser.TryParseCategoryMarker(lines[i], out var name)
                && name == category)
            {
                return i;
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
