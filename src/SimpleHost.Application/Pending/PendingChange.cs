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
    /// Writes this change to the <c>hosts</c> file through <paramref name="repository"/>, calling the
    /// matching repository operation so the on-disk file ends up as the preview projected. Mirrors
    /// <see cref="ApplyTo"/>: the projection predicts what this write performs.
    /// </summary>
    internal abstract void WriteTo(IHostsFileRepository repository);

    /// <summary>
    /// Returns the category the entry belongs in — the group whose name matches
    /// <paramref name="category"/>, or the "No category" group when it is <c>null</c>. Throws when a
    /// named category has no marker, exactly as adding to a missing category would fail on disk.
    /// </summary>
    private protected static CategoryProjection GroupFor(List<CategoryProjection> groups, string? category)
        => groups.FirstOrDefault(group => group.Name == category)
            ?? throw new InvalidOperationException($"No category marker found for '{category}'.");

    /// <summary>
    /// Locates the first entry across the groups, in file order, that equals
    /// <paramref name="target"/>, returning its group and position. Throws when no entry matches,
    /// exactly as editing a missing entry would fail on disk.
    /// </summary>
    private protected static (CategoryProjection Group, int Index) Locate(
        List<CategoryProjection> groups, HostEntry target)
    {
        foreach (var group in groups)
        {
            var index = group.Entries.IndexOf(target);
            if (index >= 0)
            {
                return (group, index);
            }
        }

        throw new InvalidOperationException($"No entry found matching '{target}'.");
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
        => GroupFor(groups, Category).Entries.Add(Entry);

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
/// A pending replacement of an existing entry with an edited one, keeping its position — and so
/// its order and category membership — unchanged.
/// </summary>
/// <param name="Target">The existing entry to replace.</param>
/// <param name="Replacement">The edited entry to put in its place.</param>
public sealed record UpdateEntryChange(HostEntry Target, HostEntry Replacement) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        var (group, index) = Locate(groups, Target);
        group.Entries[index] = Replacement;
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.UpdateEntry(Target, Replacement);
}

/// <summary>A pending deletion of an existing entry, removing it from its category.</summary>
/// <param name="Entry">The entry to delete.</param>
public sealed record DeleteEntryChange(HostEntry Entry) : PendingChange
{
    internal override void ApplyTo(List<CategoryProjection> groups)
    {
        var (group, index) = Locate(groups, Entry);
        group.Entries.RemoveAt(index);
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
        var (group, index) = Locate(groups, Entry);
        group.Entries.RemoveAt(index);
        GroupFor(groups, TargetCategory).Entries.Add(Entry);
    }

    internal override void WriteTo(IHostsFileRepository repository)
        => repository.MoveEntryToCategory(Entry, TargetCategory);
}
