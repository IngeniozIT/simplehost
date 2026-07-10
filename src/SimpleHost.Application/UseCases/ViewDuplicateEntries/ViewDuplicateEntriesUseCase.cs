using SimpleHost.Application.Ports;
using SimpleHost.Domain.Duplicates;

namespace SimpleHost.Application.UseCases.ViewDuplicateEntries;

/// <summary>
/// Reports the entries that are duplicated — the same IP address and hostname appearing more than
/// once — among the currently active entries, so the UI can warn the user about redundant entries
/// to clean up. Only active (resolving) entries are considered: a disabled entry is commented out
/// and does not resolve, so it is not counted here.
/// </summary>
public sealed class ViewDuplicateEntriesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Scans the active entries and returns the detected duplicate entries.
    /// </summary>
    public DuplicateEntries Execute() => DuplicateEntries.Of(repository.GetActiveEntries());
}
