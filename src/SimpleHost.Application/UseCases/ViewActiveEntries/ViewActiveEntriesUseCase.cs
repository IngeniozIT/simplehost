using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.ViewActiveEntries;

/// <summary>
/// Retrieves the active host entries to be displayed to the user, in file order.
/// </summary>
public sealed class ViewActiveEntriesUseCase(IHostsFileRepository repository)
{
    public IReadOnlyList<HostEntry> Execute() => repository.GetActiveEntries();
}
