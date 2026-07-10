using System.Diagnostics.CodeAnalysis;
using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Pending;

/// <summary>
/// A single edit the user has made but not yet written to the <c>hosts</c> file — one of adding,
/// updating, deleting, or moving an entry. Pending changes accumulate in a
/// <see cref="PendingChangeSet"/> and are projected onto the current entries to preview the
/// result before anything is written. Each kind knows how to apply itself, mirroring the matching
/// <see cref="Ports.IHostsFileRepository"/> operation so the preview predicts what a later apply
/// would do.
/// </summary>
public abstract record PendingChange
{
    private protected PendingChange()
    {
    }

    /// <summary>Applies this change in place to the working projection of the grouped entries.</summary>
    internal abstract void ApplyTo(List<CategoryProjection> groups);

    /// <summary>
    /// Records this change into <paramref name="changes"/>, coalescing it with the change immediately
    /// before it when both act on the same entry so the set stays minimal — a later change that
    /// supersedes the preceding one replaces it, and one that returns the entry to the state that
    /// preceding change started from cancels it. The default simply appends; only kinds that can
    /// supersede the preceding change override this. Ordering and the projected/written result are
    /// preserved. Only the immediately preceding change is considered, so a change on another entry in
    /// between keeps the two apart and never reorders unrelated edits.
    /// </summary>
    internal virtual void CoalesceInto(List<PendingChange> changes) => changes.Add(this);

    /// <summary>
    /// Writes this change to the <c>hosts</c> file through <paramref name="repository"/>, calling the
    /// matching repository operation so the on-disk file ends up as the preview projected. Mirrors
    /// <see cref="ApplyTo"/>: the projection predicts what this write performs.
    /// </summary>
    internal abstract void WriteTo(IHostsFileRepository repository);

    /// <summary>
    /// Finds the category the entry belongs in — the group whose name matches
    /// <paramref name="category"/>, or the "No category" group when it is <c>null</c> — reporting
    /// through the return value whether it exists. A change projects itself only when its category is
    /// present, so a stale change against a category that is no longer there is skipped rather than
    /// throwing, keeping the projection (and so the preview) from crashing.
    /// </summary>
    private protected static bool TryGroupFor(
        List<CategoryProjection> groups, string? category, [NotNullWhen(true)] out CategoryProjection? group)
    {
        group = groups.FirstOrDefault(candidate => candidate.Name == category);
        return group is not null;
    }

    /// <summary>
    /// Locates the first entry across the groups, in file order, that equals
    /// <paramref name="target"/>, reporting through the return value whether it was found and, when it
    /// was, its group and position. A change projects itself only when its target is still present, so
    /// a stale change whose target an earlier change already replaced or removed is skipped rather than
    /// throwing.
    /// </summary>
    private protected static bool TryLocate(
        List<CategoryProjection> groups,
        HostEntry target,
        [NotNullWhen(true)] out CategoryProjection? group,
        out int index)
    {
        foreach (var candidate in groups)
        {
            var found = candidate.Entries.IndexOf(target);
            if (found >= 0)
            {
                group = candidate;
                index = found;
                return true;
            }
        }

        // No match: callers read index only when this returns true, so it just needs a definite value.
        group = null;
        index = 0;
        return false;
    }
}

/// <summary>
/// A pending addition of a new entry, placed as the last entry under its chosen category (or in
/// "No category" when <paramref name="Category"/> is <c>null</c>).
/// </summary>
/// <param name="Entry">The entry to add.</param>
/// <param name="Category">The category to add it under, or <c>null</c> for "No category".</param>
public sealed record AddEntryChange(HostEntry Entry, string? Category) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        if (TryGroupFor(groups, Category, out var group))
        {
            group.Entries.Add(Entry);
        }
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.AddEntry(Entry, Category);
}

/// <summary>
/// A pending creation of a new, empty category, appended after every existing group — mirroring the
/// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker a later apply appends to the end of the file.
/// It carries no entries, so it projects as an empty group.
/// </summary>
/// <param name="Name">The name of the category to create.</param>
public sealed record CreateCategoryChange(string Name) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
        => groups.Add(new CategoryProjection(Name, Enumerable.Empty<HostEntry>()));

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.CreateCategory(Name);
}

/// <summary>
/// A pending rename of an existing category: its marker is rewritten with the new name while its
/// entries — and every other marker and entry — stay exactly where they are, so only the group's
/// name changes.
/// </summary>
/// <param name="CurrentName">The current name of the category to rename.</param>
/// <param name="NewName">The new name to write into the category's marker.</param>
public sealed record RenameCategoryChange(string CurrentName, string NewName) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        if (TryGroupFor(groups, CurrentName, out var group))
        {
            group.Name = NewName;
        }
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.RenameCategory(CurrentName, NewName);
}

/// <summary>
/// A pending deletion of a category: its marker is removed while the entries below it are kept and
/// fall back into "No category", appended after the existing uncategorized entries — mirroring the
/// on-disk delete, which relocates the block into the region before the first marker. Every other
/// category and its entries are left in place.
/// </summary>
/// <param name="Name">The name of the category to delete.</param>
public sealed record DeleteCategoryChange(string Name) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        if (TryGroupFor(groups, Name, out var category) && TryGroupFor(groups, null, out var noCategory))
        {
            noCategory.Entries.AddRange(category.Entries);
            groups.Remove(category);
        }
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.DeleteCategory(Name);
}

/// <summary>
/// A pending replacement of an existing entry with an edited one, keeping its position — and so
/// its order and category membership — unchanged.
/// </summary>
/// <param name="Target">The existing entry to replace.</param>
/// <param name="Replacement">The edited entry to put in its place.</param>
public sealed record UpdateEntryChange(HostEntry Target, HostEntry Replacement) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        if (TryLocate(groups, Target, out var group, out var index))
        {
            group.Entries[index] = Replacement;
        }
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.UpdateEntry(Target, Replacement);

    internal override void CoalesceInto(List<PendingChange> changes)
    {
        if (changes.Count > 0
            && changes[^1] is UpdateEntryChange previous
            && (previous.Replacement == Target || previous.Target == Target))
        {
            if (previous.Target == Replacement)
            {
                // The entry returns to the state the preceding update started from: the two cancel.
                changes.RemoveAt(changes.Count - 1);
            }
            else
            {
                // Supersede the preceding update, keeping its original target so the net edit still
                // resolves against the same on-disk (or earlier-staged) entry, in the same position.
                changes[^1] = new UpdateEntryChange(previous.Target, Replacement);
            }

            return;
        }

        changes.Add(this);
    }
}

/// <summary>A pending deletion of an existing entry, removing it from its category.</summary>
/// <param name="Entry">The entry to delete.</param>
public sealed record DeleteEntryChange(HostEntry Entry) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        if (TryLocate(groups, Entry, out var group, out var index))
        {
            group.Entries.RemoveAt(index);
        }
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.DeleteEntry(Entry);
}

/// <summary>
/// A pending move of an existing entry into a different category, appended as the last entry of
/// the target category (or of "No category" when <paramref name="TargetCategory"/> is <c>null</c>).
/// </summary>
/// <param name="Entry">The entry to move.</param>
/// <param name="TargetCategory">The category to move it into, or <c>null</c> for "No category".</param>
public sealed record MoveEntryChange(HostEntry Entry, string? TargetCategory) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        // Resolve both the source entry and the destination category before mutating, so a stale move
        // whose entry or target category is gone is skipped whole rather than half-applied.
        if (TryLocate(groups, Entry, out var group, out var index)
            && TryGroupFor(groups, TargetCategory, out var target))
        {
            group.Entries.RemoveAt(index);
            target.Entries.Add(Entry);
        }
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.MoveEntryToCategory(Entry, TargetCategory);
}

/// <summary>
/// A pending reorder of an existing entry to a new position within the category it already belongs
/// to — the drag-and-drop reorder of the list — leaving its category membership and content
/// unchanged.
/// </summary>
/// <param name="Entry">The entry to reorder.</param>
/// <param name="TargetIndex">
/// The zero-based position the entry should occupy among its category's entries.
/// </param>
public sealed record ReorderEntryChange(HostEntry Entry, int TargetIndex) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        if (TryLocate(groups, Entry, out var group, out var index))
        {
            group.Entries.RemoveAt(index);
            group.Entries.Insert(TargetIndex, Entry);
        }
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.ReorderEntry(Entry, TargetIndex);
}
