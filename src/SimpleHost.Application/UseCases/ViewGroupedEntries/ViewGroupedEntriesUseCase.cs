using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.UseCases.ViewGroupedEntries;

/// <summary>
/// Retrieves the host entries grouped by category for display: the always-first "No category"
/// group followed by each category in file order, so the user sees their entries organised
/// under the categories they belong to.
/// </summary>
public sealed class ViewGroupedEntriesUseCase(IHostsFileRepository repository)
{
    public IReadOnlyList<CategoryGroup> Execute() => repository.GetGroupedEntries();
}
