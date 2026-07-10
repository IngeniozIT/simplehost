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

    /// <summary>
    /// Raised whenever the set of pending changes changes — a change is recorded or the set is
    /// cleared — so a view-model can keep a live count (and derived state such as whether an apply
    /// or discard is possible) in sync without polling.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>The accumulated changes, in the order they were made.</summary>
    public IReadOnlyList<PendingChange> Changes => _changes;

    /// <summary>Whether any change is pending; <see langword="false"/> means the set is a no-op.</summary>
    public bool HasChanges => _changes.Count > 0;

    /// <summary>Records adding <paramref name="entry"/> under <paramref name="category"/> (or "No category").</summary>
    public void AddEntry(HostEntry entry, string? category = null)
        => Record(new AddEntryChange(entry, category));

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
        Record(new CreateCategoryChange(name.Trim()));
    }

    /// <summary>
    /// Records renaming the category <paramref name="currentName"/> to <paramref name="newName"/>
    /// (trimmed of surrounding whitespace), which projects as the same group under its new name with
    /// its entries unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="currentName"/> or <paramref name="newName"/> is <c>null</c>,
    /// empty, or whitespace only.
    /// </exception>
    public void RenameCategory(string currentName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        Record(new RenameCategoryChange(currentName, newName.Trim()));
    }

    /// <summary>
    /// Records deleting the category <paramref name="name"/>, which projects as removing that group
    /// and moving its entries back into "No category".
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public void DeleteCategory(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Record(new DeleteCategoryChange(name));
    }

    /// <summary>Records replacing <paramref name="target"/> in place with <paramref name="replacement"/>.</summary>
    public void UpdateEntry(HostEntry target, HostEntry replacement)
        => Record(new UpdateEntryChange(target, replacement));

    /// <summary>Records deleting <paramref name="entry"/>.</summary>
    public void DeleteEntry(HostEntry entry)
        => Record(new DeleteEntryChange(entry));

    /// <summary>Records moving <paramref name="entry"/> into <paramref name="targetCategory"/> (or "No category").</summary>
    public void MoveEntry(HostEntry entry, string? targetCategory)
        => Record(new MoveEntryChange(entry, targetCategory));

    /// <summary>
    /// Records reordering <paramref name="entry"/> to the zero-based position
    /// <paramref name="targetIndex"/> among the entries of the category it already belongs to.
    /// </summary>
    public void ReorderEntry(HostEntry entry, int targetIndex)
        => Record(new ReorderEntryChange(entry, targetIndex));

    /// <summary>
    /// Discards every accumulated change, leaving the set empty and <see cref="HasChanges"/>
    /// <see langword="false"/> again. Used once the changes have been written to the file so they
    /// are no longer pending.
    /// </summary>
    public void Clear()
    {
        _changes.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Record(PendingChange change)
    {
        _changes.Add(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }

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
