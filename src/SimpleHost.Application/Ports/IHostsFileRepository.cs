using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application access to the entries stored in the <c>hosts</c> file.
/// Implemented by infrastructure adapters that know the on-disk format.
/// </summary>
public interface IHostsFileRepository
{
    /// <summary>
    /// Adds a new entry to the <c>hosts</c> file, placing it under the chosen category's
    /// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker block — appended as the last entry of
    /// that block — or, when <paramref name="category"/> is <c>null</c>, in the "No category"
    /// region (before the first marker) so it becomes part of the stored entries and is
    /// returned by subsequent reads.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    /// <param name="category">
    /// The name of an existing category to place the entry under, or <c>null</c> to leave it
    /// uncategorized ("No category").
    /// </param>
    void AddEntry(HostEntry entry, string? category = null);

    /// <summary>
    /// Creates a new, empty category by appending a <c># [SimpleHost:Category] &lt;Name&gt;</c>
    /// marker to the <c>hosts</c> file, after all existing content, so it becomes the last group.
    /// The marker carries no entries, so the category is returned by subsequent reads as an empty
    /// group. Whether the same name already exists is not checked here; callers own that decision.
    /// </summary>
    /// <param name="name">The name of the category to create.</param>
    void CreateCategory(string name);

    /// <summary>
    /// Renames an existing category by rewriting its
    /// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker with <paramref name="newName"/> — the first
    /// such marker when the name repeats. Only the marker line changes: the entries below it stay in
    /// place, so they remain under the (now renamed) category, and every other marker and entry is
    /// left untouched. Whether the new name collides with an existing category is not checked here;
    /// callers own that decision. Throws when no marker matches <paramref name="currentName"/>.
    /// </summary>
    /// <param name="currentName">The current name of the category to rename.</param>
    /// <param name="newName">The new name to write into the category's marker.</param>
    void RenameCategory(string currentName, string newName);

    /// <summary>
    /// Deletes a category by removing its <c># [SimpleHost:Category] &lt;Name&gt;</c> marker — the
    /// first such marker when the name repeats — while keeping every entry that was under it. Those
    /// entries are relocated into the "No category" region (before the first remaining marker), so
    /// they fall back to "No category" rather than being lost or absorbed by a neighbouring category.
    /// Every other marker and entry is left in place. Throws when no marker matches
    /// <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the category to delete.</param>
    void DeleteCategory(string name);

    /// <summary>
    /// Replaces the first entry that matches <paramref name="target"/> with
    /// <paramref name="replacement"/>, rewriting that entry's <c>hosts</c>-file line in place so
    /// its position — and therefore its order and category membership — is preserved. Used to
    /// edit an existing entry, such as changing its IP address while keeping its hostname,
    /// comment, and active/disabled status.
    /// </summary>
    /// <param name="target">The existing entry to replace, as previously read from the file.</param>
    /// <param name="replacement">The entry to write in its place.</param>
    void UpdateEntry(HostEntry target, HostEntry replacement);

    /// <summary>
    /// Moves the first entry that matches <paramref name="entry"/> under the
    /// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker block named by
    /// <paramref name="targetCategory"/> — appended as the last entry of that block — or, when
    /// <paramref name="targetCategory"/> is <c>null</c>, into the "No category" region (before the
    /// first marker). The entry's line is relocated with its content preserved (IP, hostname,
    /// inline comment, and active/disabled status), so its category membership changes while the
    /// entry itself is unchanged.
    /// </summary>
    /// <param name="entry">The existing entry to move, as previously read from the file.</param>
    /// <param name="targetCategory">
    /// The name of an existing category to move the entry under, or <c>null</c> to move it into
    /// "No category".
    /// </param>
    void MoveEntryToCategory(HostEntry entry, string? targetCategory);

    /// <summary>
    /// Reorders the first entry that matches <paramref name="entry"/> to the zero-based position
    /// <paramref name="targetIndex"/> among the entries of the category block it already belongs to,
    /// rewriting that block's entry lines in the new order so the change is persisted purely by line
    /// order — there is no separate ordering metadata. Only the entry lines of that one block are
    /// reordered: the category's <c># [SimpleHost:Category] &lt;Name&gt;</c> marker, any blank lines
    /// or plain comments interleaved within the block, and every other category and its entries are
    /// left in place; the entry's own content (IP, hostname, inline comment, and active/disabled
    /// status) is unchanged. <paramref name="targetIndex"/> counts entries only — active and
    /// disabled (commented-out) alike — in file order, starting at 0. Throws
    /// <see cref="InvalidOperationException"/> when no entry matches, and
    /// <see cref="ArgumentOutOfRangeException"/> when <paramref name="targetIndex"/> falls outside
    /// the range of the block's entries.
    /// </summary>
    /// <param name="entry">The existing entry to reorder, as previously read from the file.</param>
    /// <param name="targetIndex">
    /// The zero-based position the entry should occupy among its category's entries after the move.
    /// </param>
    void ReorderEntry(HostEntry entry, int targetIndex);

    /// <summary>
    /// Removes the first entry that matches <paramref name="entry"/> from the <c>hosts</c> file,
    /// deleting its line so the entry disappears from subsequent reads. Matching is by parsed
    /// content (IP, hostname, inline comment, active/disabled status) rather than raw text, so a
    /// disabled (commented-out) entry can be deleted too. Any surrounding lines — other entries,
    /// blank lines, plain comments, and <c># [SimpleHost:Category] &lt;Name&gt;</c> markers — are
    /// left in place, so a category whose last entry is deleted keeps its (now empty) marker.
    /// Throws when no line matches. Deleting a system entry is a destructive action the UI must
    /// confirm before calling.
    /// </summary>
    /// <param name="entry">The existing entry to delete, as previously read from the file.</param>
    void DeleteEntry(HostEntry entry);

    /// <summary>
    /// Returns every active (uncommented) entry currently in the <c>hosts</c> file,
    /// in the order they appear.
    /// </summary>
    IReadOnlyList<HostEntry> GetActiveEntries();

    /// <summary>
    /// Returns every entry currently in the <c>hosts</c> file — active and disabled
    /// (commented-out) alike — in the order they appear, each carrying its
    /// <see cref="HostEntry.Status"/>.
    /// </summary>
    IReadOnlyList<HostEntry> GetEntries();

    /// <summary>
    /// Returns the entries grouped by the category they belong to. The first group is always
    /// the "No category" group (its <see cref="CategoryGroup.Name"/> is <c>null</c>), holding
    /// the entries that appear before any <c># [SimpleHost:Category] &lt;Name&gt;</c> marker;
    /// the remaining groups follow in the order their markers appear in the file, each carrying
    /// its entries in file order.
    /// </summary>
    IReadOnlyList<CategoryGroup> GetGroupedEntries();
}
