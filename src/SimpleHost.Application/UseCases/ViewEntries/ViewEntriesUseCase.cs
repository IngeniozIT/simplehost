using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.ViewEntries;

/// <summary>
/// Retrieves all host entries to be displayed to the user — active and disabled — in file
/// order, so disabled entries appear in the list alongside active ones, distinguishable by
/// their <see cref="HostEntry.Status"/>.
/// </summary>
public sealed class ViewEntriesUseCase(IHostsFileRepository repository)
{
    public IReadOnlyList<HostEntry> Execute() => repository.GetEntries();
}
