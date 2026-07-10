using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.UpdateEntryHostname;

/// <summary>
/// Changes the hostname of an existing host entry, persisting the change through the
/// repository so the entry's <c>hosts</c>-file line is rewritten and the new hostname appears
/// in the list. The entry keeps its IP address, inline comment, and active/disabled status.
/// </summary>
public sealed class UpdateEntryHostnameUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces <paramref name="entry"/> with a copy carrying <paramref name="newHostname"/> in
    /// place of its current hostname, stores the change, and returns the updated entry.
    /// </summary>
    /// <param name="entry">The existing entry whose hostname is being changed.</param>
    /// <param name="newHostname">The hostname the entry's IP address should now map.</param>
    public HostEntry Execute(HostEntry entry, string newHostname)
    {
        var updated = entry with { Hostname = newHostname };
        repository.UpdateEntry(entry, updated);
        return updated;
    }
}
