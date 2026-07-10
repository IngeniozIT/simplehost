using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Pending;

/// <summary>
/// The set of edits the user has made but not yet written to the <c>hosts</c> file, accumulated
/// in the order they were made. Nothing here touches the file: the set records intent, and
/// <see cref="ApplyTo"/> projects that intent onto a snapshot of the current entries so the result
/// can be previewed before any write happens.
/// </summary>
public sealed class PendingChangeSet
{
    private readonly List<PendingChange> _changes = new();

    /// <summary>The accumulated changes, in the order they were made.</summary>
    public IReadOnlyList<PendingChange> Changes => _changes;

    /// <summary>Whether any change is pending; <see langword="false"/> means the set is a no-op.</summary>
    public bool HasChanges => _changes.Count > 0;

    /// <summary>Records adding <paramref name="entry"/> under <paramref name="category"/> (or "No category").</summary>
    public void AddEntry(HostEntry entry, string? category = null)
        => _changes.Add(new AddEntryChange(entry, category));

    /// <summary>
    /// Records creating a new, empty category named <paramref name="name"/> (trimmed of surrounding
    /// whitespace), which projects as an empty group appended after the existing groups.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public void CreateCategory(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _changes.Add(new CreateCategoryChange(name.Trim()));
    }

    /// <summary>Records replacing <paramref name="target"/> in place with <paramref name="replacement"/>.</summary>
    public void UpdateEntry(HostEntry target, HostEntry replacement)
        => _changes.Add(new UpdateEntryChange(target, replacement));

    /// <summary>Records deleting <paramref name="entry"/>.</summary>
    public void DeleteEntry(HostEntry entry)
        => _changes.Add(new DeleteEntryChange(entry));

    /// <summary>Records moving <paramref name="entry"/> into <paramref name="targetCategory"/> (or "No category").</summary>
    public void MoveEntry(HostEntry entry, string? targetCategory)
        => _changes.Add(new MoveEntryChange(entry, targetCategory));

    /// <summary>
    /// Discards every accumulated change, leaving the set empty and <see cref="HasChanges"/>
    /// <see langword="false"/> again. Used once the changes have been written to the file so they
    /// are no longer pending.
    /// </summary>
    public void Clear() => _changes.Clear();

    /// <summary>
    /// Projects the accumulated changes onto <paramref name="current"/> and returns the resulting
    /// grouped entries — what the file would hold once the changes are applied — without mutating
    /// the input or writing anything. Applying an empty set returns the current grouping unchanged.
    /// </summary>
    /// <param name="current">The grouped entries as they are on disk now.</param>
    public IReadOnlyList<CategoryGroup> ApplyTo(IReadOnlyList<CategoryGroup> current)
    {
        var projection = current
            .Select(group => new CategoryProjection(group.Name, group.Entries))
            .ToList();

        foreach (var change in _changes)
        {
            change.ApplyTo(projection);
        }

        return projection.Select(group => group.ToCategoryGroup()).ToList();
    }
}
