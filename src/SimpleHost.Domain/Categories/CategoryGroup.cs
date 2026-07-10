using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Categories;

/// <summary>
/// A category and the host entries that belong to it, as grouped for display. Categories are
/// introduced in the <c>hosts</c> file by <c># [SimpleHost:Category] &lt;Name&gt;</c> markers;
/// every entry line below a marker, until the next marker, belongs to that category.
/// </summary>
/// <param name="Name">
/// The category name, or <c>null</c> for the special "No category" group that holds entries
/// appearing before any marker. The "No category" group is always shown pinned to the top.
/// </param>
/// <param name="Entries">
/// The entries belonging to this category, in file order — active and disabled alike. May be
/// empty when a category marker has no entries below it.
/// </param>
public sealed record CategoryGroup(string? Name, IReadOnlyList<HostEntry> Entries);
