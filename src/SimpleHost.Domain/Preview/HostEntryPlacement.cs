using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Preview;

/// <summary>
/// A single host entry together with the category it belongs to, as it sits on one line of the
/// <c>hosts</c> file. Because entry order and category membership map directly to line order,
/// the ordered sequence of placements is a faithful, marker-free projection of the file's
/// entry lines — the shape a pending-changes preview diffs over.
/// </summary>
/// <param name="Category">
/// The name of the category the entry sits under, or <c>null</c> for the "No category" region
/// before the first marker.
/// </param>
/// <param name="Entry">The entry occupying the line.</param>
public sealed record HostEntryPlacement(string? Category, HostEntry Entry)
{
    /// <summary>
    /// Flattens grouped entries into the ordered sequence of placements they occupy in the file:
    /// each group's entries, in file order, paired with the group's category name. Empty groups
    /// (category markers with no entries) contribute nothing, since they hold no entry lines.
    /// </summary>
    public static IReadOnlyList<HostEntryPlacement> Flatten(IReadOnlyList<CategoryGroup> groups)
        => groups
            .SelectMany(group => group.Entries.Select(entry => new HostEntryPlacement(group.Name, entry)))
            .ToList();
}
