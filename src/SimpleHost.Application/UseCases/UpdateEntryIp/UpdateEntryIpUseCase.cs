using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.UpdateEntryIp;

/// <summary>
/// Changes the IP address of an existing host entry, persisting the change through the
/// repository so the entry's <c>hosts</c>-file line is rewritten and the new IP appears in the
/// list. The entry keeps its hostname, inline comment, and active/disabled status.
/// </summary>
public sealed class UpdateEntryIpUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces <paramref name="entry"/> with a copy carrying <paramref name="newIpAddress"/> in
    /// place of its current IP address, stores the change, and returns the updated entry.
    /// </summary>
    /// <param name="entry">The existing entry whose IP address is being changed.</param>
    /// <param name="newIpAddress">The IP address the entry's hostname should now resolve to.</param>
    public HostEntry Execute(HostEntry entry, string newIpAddress)
    {
        var updated = entry with { IpAddress = newIpAddress };
        repository.UpdateEntry(entry, updated);
        return updated;
    }
}
