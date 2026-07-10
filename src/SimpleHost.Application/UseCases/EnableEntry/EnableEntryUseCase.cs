using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.EnableEntry;

/// <summary>
/// Re-enables a disabled host entry so it resolves again, persisting the change through the
/// repository. The entry's line is rewritten in place with the leading <c>#</c> that was
/// disabling it removed, so it keeps its position — and therefore its order and category
/// membership — while its content (IP address, hostname, inline comment) is preserved and only
/// its status becomes <see cref="HostEntryStatus.Active"/>.
/// </summary>
public sealed class EnableEntryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces <paramref name="entry"/> with a copy marked
    /// <see cref="HostEntryStatus.Active"/>, stores the change, and returns the enabled entry.
    /// </summary>
    /// <param name="entry">The existing entry to enable, as previously read from the file.</param>
    public HostEntry Execute(HostEntry entry)
    {
        var enabled = entry with { Status = HostEntryStatus.Active };
        repository.UpdateEntry(entry, enabled);
        return enabled;
    }
}
