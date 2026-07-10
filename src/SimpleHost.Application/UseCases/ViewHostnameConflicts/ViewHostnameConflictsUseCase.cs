using SimpleHost.Application.Ports;
using SimpleHost.Domain.Conflicts;

namespace SimpleHost.Application.UseCases.ViewHostnameConflicts;

/// <summary>
/// Reports the hostnames that resolve ambiguously — mapped to more than one distinct IP address —
/// among the currently active entries, so the UI can warn the user about conflicts to resolve.
/// Only active (resolving) entries are considered: a disabled entry is commented out and does not
/// resolve, so it cannot conflict.
/// </summary>
public sealed class ViewHostnameConflictsUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Scans the active entries and returns the detected hostname/IP conflicts.
    /// </summary>
    public HostnameConflicts Execute() => HostnameConflicts.Of(repository.GetActiveEntries());
}
