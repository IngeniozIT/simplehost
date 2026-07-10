using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.FilterEntriesByCategory;

/// <summary>
/// Narrows the displayed host entries to those belonging to a single chosen category — active
/// and disabled alike, in file order — so a user can focus on one category at a time.
/// </summary>
/// <remarks>
/// The category is chosen from those that already exist, so matching is an exact comparison
/// against the category name. A <c>null</c> category selects the special "No category" group of
/// entries that appear before any marker. A category that holds no entries, or one that does not
/// exist, narrows to an empty list.
/// </remarks>
public sealed class FilterEntriesByCategoryUseCase(IHostsFileRepository repository)
{
    public IReadOnlyList<HostEntry> Execute(string? category)
        => repository.GetGroupedEntries()
            .Where(group => group.Name == category)
            .SelectMany(group => group.Entries)
            .ToList();
}
