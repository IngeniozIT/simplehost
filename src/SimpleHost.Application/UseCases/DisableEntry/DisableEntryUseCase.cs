using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.DisableEntry;

/// <summary>
/// Disables an active host entry so it stays in the <c>hosts</c> file but no longer resolves,
/// persisting the change through the repository. The entry's line is rewritten in place as a
/// commented-out (<c>#</c>) line, so it keeps its position — and therefore its order and
/// category membership — while its content (IP address, hostname, inline comment) is preserved
/// and only its status becomes <see cref="HostEntryStatus.Disabled"/>.
/// </summary>
public sealed class DisableEntryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces <paramref name="entry"/> with a copy marked
    /// <see cref="HostEntryStatus.Disabled"/>, stores the change, and returns the disabled entry.
    /// </summary>
    /// <param name="entry">The existing entry to disable, as previously read from the file.</param>
    public HostEntry Execute(HostEntry entry)
    {
        var disabled = entry with { Status = HostEntryStatus.Disabled };
        repository.UpdateEntry(entry, disabled);
        return disabled;
    }
}
