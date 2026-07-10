using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Pending;

/// <summary>
/// A mutable working copy of one category and its entries, used while projecting pending changes
/// onto the grouped entries without touching the file. Mirrors a <see cref="CategoryGroup"/> —
/// including empty groups, whose (now empty) marker is preserved exactly as an on-disk delete
/// would leave it — and is converted back once every change has been applied.
/// </summary>
internal sealed class CategoryProjection
{
    internal CategoryProjection(string? name, IEnumerable<HostEntry> entries)
    {
        Name = name;
        Entries = entries.ToList();
    }

    /// <summary>
    /// The category name, or <c>null</c> for the "No category" region. Settable so a pending rename
    /// can rewrite the marker in place while its entries stay put.
    /// </summary>
    internal string? Name { get; set; }

    /// <summary>The entries under this category, in file order, mutated as changes are applied.</summary>
    internal List<HostEntry> Entries { get; }

    internal CategoryGroup ToCategoryGroup() => new(Name, Entries);
}
