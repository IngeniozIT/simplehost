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
    /// Adds a new entry to the <c>hosts</c> file, appending it as a new line so it becomes
    /// part of the stored entries and is returned by subsequent reads.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    void AddEntry(HostEntry entry);

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
